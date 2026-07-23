# Contract & Component Testing in .NET

A small four-service system used to explore **component testing** and **consumer-driven contract testing**, and how the two fit together.

The short version of why both:

- A **component test** runs one service in isolation with its collaborators stubbed. It proves the service behaves correctly *given* a particular response — but the stub encodes an assumption nobody checks.
- A **contract test** verifies that assumption against the real provider. It does not prove your service works; it proves the response you stubbed is one the provider can actually produce.

Component tests validate behaviour. Contract tests validate the stubs those behaviour tests depend on.

## The system

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

| Service | Role | Port |
| --- | --- | --- |
| `OrderService` | Consumer. Places orders, calls both providers, publishes `OrderPlaced`. | 5000 |
| `InventoryService` | HTTP provider. Stock levels and prices. | 5001 |
| `PaymentService` | HTTP provider. Authorises payments; declines above a credit limit. | 5002 |
| `NotificationService` | Message consumer for `OrderPlaced`. | — |

`POST /orders` exercises every interesting path:

| Scenario | Result |
| --- | --- |
| All SKUs known and in stock, payment authorised | `201 Created` |
| Unknown SKU | `400 Bad Request` |
| Insufficient stock | `409 Conflict` |
| Order total above the credit limit | `402 Payment Required` |

## Running it locally

Start the infrastructure (PostgreSQL + RabbitMQ):

```bash
docker compose up -d
```

Then run the services, each in its own terminal:

```bash
dotnet run --project src/InventoryService
```

```bash
dotnet run --project src/PaymentService
```

```bash
dotnet run --project src/OrderService
```

```bash
dotnet run --project src/NotificationService
```

Databases are created and migrated on startup, and the inventory is seeded with `SKU-COFFEE`, `SKU-MUG` and `SKU-GRINDER`.

### Sending requests

[`requests.http`](requests.http) covers every scenario and runs directly in Rider, Visual Studio and VS Code. That is the easiest way in.

From a shell, on macOS or Linux:

```bash
curl -X POST http://localhost:5000/orders -H "Content-Type: application/json" -d '{"customerId":"cust-1","items":[{"sku":"SKU-COFFEE","quantity":2}]}'
```

On Windows PowerShell, two things bite. `curl` is an alias for `Invoke-WebRequest`, so call `curl.exe` explicitly; and PowerShell rewrites quoting on its way to native programs, so prefix the arguments with `--%` to pass them through untouched:

```bash
curl.exe --% -X POST http://localhost:5000/orders -H "Content-Type: application/json" -d "{\"customerId\":\"cust-1\",\"items\":[{\"sku\":\"SKU-COFFEE\",\"quantity\":2}]}"
```

## Stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 10, minimal APIs |
| Persistence | EF Core + PostgreSQL |
| Messaging | MassTransit 8 + RabbitMQ |
| Resilience | `Microsoft.Extensions.Http.Resilience` on both typed clients |

MassTransit is pinned to 8.x deliberately — version 9 moved to a commercial licence.

## Where the test seams are

The design keeps three seams deliberately visible, because the tests attach to them:

- **`OrderService.Clients.InventoryClient` / `PaymentClient`** — typed `HttpClient` wrappers rather than generated clients. Consumer contract tests point these at a mock provider.
- **`Program` is `public partial`** in each API, so `WebApplicationFactory<Program>` can host the service in-process for component tests.
- **`OrderPlacementService`** holds the placement logic, keeping endpoints thin and the business rules addressable.
