#!/usr/bin/env bash
# Publishes every pact in pacts/ to the local broker.
#
# The consumer version is the git SHA and the branch is the git branch, so the
# broker can answer "has this exact commit been verified" rather than "has
# something called OrderService been verified at some point".
set -euo pipefail

BROKER_URL="${PACT_BROKER_BASE_URL:-http://host.docker.internal:9292}"

# The CLI runs inside a container, where localhost is the container itself. Callers
# who set PACT_BROKER_BASE_URL for the verifier, which runs on the host, would
# otherwise silently fail to reach the broker from here.
BROKER_URL="${BROKER_URL/localhost/host.docker.internal}"
BROKER_URL="${BROKER_URL/127.0.0.1/host.docker.internal}"
BROKER_USER="${PACT_BROKER_USERNAME:-pact}"
BROKER_PASS="${PACT_BROKER_PASSWORD:-pact}"
VERSION="${GIT_COMMIT:-$(git rev-parse --short HEAD)}"
BRANCH="${GIT_BRANCH:-$(git rev-parse --abbrev-ref HEAD)}"

echo "Publishing pacts as version ${VERSION} on branch ${BRANCH}"

# MSYS_NO_PATHCONV stops Git Bash on Windows rewriting container paths like /pacts
# into host paths. It is ignored everywhere else.
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "$(pwd)/pacts":/pacts \
  --add-host host.docker.internal:host-gateway \
  pactfoundation/pact-cli:latest \
  publish /pacts \
    --broker-base-url "${BROKER_URL}" \
    --broker-username "${BROKER_USER}" \
    --broker-password "${BROKER_PASS}" \
    --consumer-app-version "${VERSION}" \
    --branch "${BRANCH}"
