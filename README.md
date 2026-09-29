# Contract & Component Testing in .NET

A worked example of **component testing** and **consumer-driven contract testing** on a four-service .NET system — and, more importantly, of what each layer can and cannot catch.

The premise in one paragraph:

> A **component test** runs one service in isolation with its collaborators stubbed. It proves the service behaves correctly *given* a particular response — but you wrote that stub, and nothing checks it. A **contract test** checks the stub against the real provider. It does not prove your service works; it proves the response you stubbed is one the provider can actually produce.
>
> **Component tests validate behaviour. Contract tests validate the stubs those behaviour tests depend on.**

---

## What this repository demonstrates

| Layer | Owns | Blind to |
| --- | --- | --- |
| **Component** | Your logic, call ordering, retries and timeouts | Everything a provider actually does |
| **Consumer pact** | What you send, and what you need back | Whether the provider agrees |
| **Provider verification** | Whether the provider can deliver it | What consumers do with the result |
| **`can-i-deploy`** | Whether the system agrees *right now* | Whether any of it is correct |

None of them substitutes for another. The expensive mistakes come from believing one covers a neighbour's job.

### The break/catch matrix

Every cell below is a measured run: a defect was introduced into the code, all six suites were executed, and the result recorded.

| | Defect | Inventory<br>component | Payment<br>component | Order<br>component | Consumer<br>pacts | Inventory<br>verify | Payment<br>verify |
| --- | --- | :-: | :-: | :-: | :-: | :-: | :-: |
| A | Order total computed wrong | – | – | **FAIL** | pass | – | – |
| B | Inventory renames a response field | *build* | – | **pass** | pass | **FAIL** | – |
| C | Inventory **adds** a response field | pass | – | pass | pass | pass | – |
| D | Payment declines with 400 instead of 422 | – | **FAIL** | pass | pass | – | **FAIL** |
| E | Order stops sending a required field | – | – | pass | **FAIL** | – | pass |
| F | Payment changes a status string's casing | – | **pass** | pass | pass | – | **FAIL** |
| G | Order's HTTP client becomes a strict JSON reader | – | – | **FAIL** | pass | – | – |
| H | A consumer pact pins exact values | – | – | – | pass | **FAIL** | – |

Four rows are worth the price of the repository:

- **Row B** — the consumer's entire component suite stays green while the consumer is broken. This is the gap contract testing exists to close, and nothing else in the matrix closes it.
- **Row C** — everything passes, and it *must*. A provider adding a field is the most common change it will ever make. A contract that breaks here is a distributed schema lock, not a safety net.
- **Row F** — the provider's own tests pass, because they assert against the same constant the production code uses. A test comparing code to itself proves nothing; only the contract, holding the literal string, sees the drift.
- **Row H** — a badly written contract fails on *the other team's* build. That is the social cost of pinning exact values instead of types.

---

## The system

Deliberately boring. The interesting part is the test topology.

```
                    ┌──────────────────┐
              HTTP  │ InventoryService │  GET /stock/{sku}
         ┌─────────▶│    (provider)    │
         │          └──────────────────┘
┌────────┴──────┐
│ OrderService  │   HTTP  ┌──────────────────┐
│  (consumer)   │────────▶│  PaymentService  │  POST /payments
└────────┬──────┘         │    (provider)    │
         │                └──────────────────┘
         │ publishes OrderPlaced
         ▼
┌─────────────────────┐
│ NotificationService │
│ (message consumer)  │
└─────────────────────┘
```

`POST /orders` fans out to both providers and publishes an event, which gives every scenario worth testing:

| Scenario | Result |
| --- | --- |
| SKUs known and in stock, payment authorised | `201 Created` + `OrderPlaced` published |
| Unknown SKU | `400 Bad Request` |
| Insufficient stock | `409 Conflict`, payment never called |
| Total above the credit limit | `402 Payment Required`, no event published |

---

## The tests

**29 tests across 8 projects**, plus 3 contracts.

| Project | Tests | What it covers |
| --- | :-: | --- |
| `InventoryService.ComponentTests` | 2 | Stock endpoint against real PostgreSQL |
| `PaymentService.ComponentTests` | 8 | Credit-limit boundary, persistence, route constraints |
| `OrderService.ComponentTests` | 11 | Fan-out, all four outcomes, retries, timeouts, tolerant reading |
| `OrderService.ContractTests` | 4 | Consumer pacts for Inventory and Payment |
| `NotificationService.ContractTests` | 1 | Consumer pact for the `OrderPlaced` message |
| `InventoryService.ContractTests` | 1 | Provider verification, with provider states |
| `PaymentService.ContractTests` | 1 | Provider verification |
| `OrderService.MessageContractTests` | 1 | Message provider verification |

### Techniques worth looking at

- **[`PactStubs.cs`](tests/OrderService.ComponentTests/PactStubs.cs)** — component stubs are built *from the pact files*, so a stub cannot describe a response the provider never agreed to. Overriding a field the contract does not record throws with a diagnostic. The pact owns the shape; the test owns the values.
- **[`OutageTests.cs`](tests/OrderService.ComponentTests/OutageTests.cs)** — 503s, timeouts and malformed bodies, which a contract *structurally cannot* express: there is no message for a provider to verify. Includes a characterisation test recording that `POST /payments` is retried four times despite not being idempotent.
- **[`InventoryProviderFactory.cs`](tests/InventoryService.ContractTests/InventoryProviderFactory.cs)** — provider states served from a test-only endpoint, with a real database reset between interactions.
- **[`OrderPlacedProviderTests.cs`](tests/OrderService.MessageContractTests/OrderPlacedProviderTests.cs)** — the message verification places a *real* order and verifies the event actually published, rather than one the test constructed.
- **[`PactSource.cs`](tests/ContractTesting.Support/PactSource.cs)** — contracts come from a broker in CI and from committed files locally, so the suite runs with no infrastructure.

### Isolation, the recurring theme

Four different shared things leaked between tests while this was built, each caught and fixed:

| Shared thing | How it leaked | Fix |
| --- | --- | --- |
| In-memory database | Two classes sharing a name saw each other's rows | One store per fixture |
| PostgreSQL container | Rows survived between tests | Respawn before every test |
| WireMock request log | Abandoned attempts logged *after* a reset, counted against the next test | Scope assertions by path, one SKU per test |
| MassTransit test harness | Keeps every message since host start; a reset does not clear it | Scope assertions to the test's own order |

The general rule: **when state can arrive late, isolate by scoping your assertions, not only by clearing state beforehand.**

---

## Running it

### Tests

Requires Docker — the suites start their own PostgreSQL via Testcontainers.

```bash
dotnet test
```

No broker needed: provider verification reads the committed pacts unless `PACT_BROKER_BASE_URL` is set.

### The system

```bash
docker compose up -d
```

Starts PostgreSQL, RabbitMQ and a Pact Broker (`http://localhost:9292`, `pact`/`pact`). Then run each service in its own terminal:

```bash
dotnet run --project src/InventoryService   # :5001
dotnet run --project src/PaymentService     # :5002
dotnet run --project src/OrderService       # :5000
dotnet run --project src/NotificationService
```

Databases are created and migrated on startup; inventory is seeded with `SKU-COFFEE`, `SKU-MUG` and `SKU-GRINDER`.

[`requests.http`](requests.http) covers every scenario and runs in Rider, Visual Studio and VS Code. A [Postman collection](postman/contract-and-component-testing.postman_collection.json) is included, with assertions on every request.

### The broker

```bash
bash scripts/publish-pacts.sh              # publishes with the git sha as the version
bash scripts/can-i-deploy.sh OrderService  # exits non-zero if nothing has verified it
```

---

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) — three jobs, not one:

```
component-tests            consumer-contracts
(depends on nothing)       generates + uploads pacts
                                    │
                                    ▼
                          provider-verification
                          fetch from broker, verify, publish results
                                    │
                                    ▼
                               can-i-deploy
```

The consumer stage never verifies a provider. In a real system it could not — that is another repository's pipeline — so the pacts move by artifact, standing in for a broker webhook. Component tests depend on nothing, because a contract problem should not make your own logic look broken.

`consumer-contracts` also fails the build if the committed pacts differ from the generated ones. Anything both committed and generated can go stale.

---

## Stack

| Concern | Choice | Why |
| --- | --- | --- |
| Runtime | .NET 10, minimal APIs | |
| Tests | xUnit v3 + Shouldly | FluentAssertions is commercially licensed from v8 |
| Hosting under test | `WebApplicationFactory` | In-process for component tests, real Kestrel for provider verification |
| Databases | Testcontainers + Respawn | In-memory EF ignores constraints, migrations and column types |
| HTTP stubs | WireMock.Net | Real HTTP over a real socket, so the client and its resilience pipeline run |
| Contracts | PactNet 5 | |
| Messaging | MassTransit 8 | Version 9 moved to a commercial licence |

### Design decisions that exist for testing

- **Typed `HttpClient` wrappers**, hand-written rather than generated — consumer pacts attach to that seam precisely, keeping contracts minimal.
- **`public partial class Program`** in each API, so `WebApplicationFactory<Program>` can reach it. Top-level statements otherwise compile it as `internal`.
- **Business rules in a service class**, not in endpoints, so they are addressable without going through a route.
- **`TimeProvider` injected**, so timestamps are controllable.
