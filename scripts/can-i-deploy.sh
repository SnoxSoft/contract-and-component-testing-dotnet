#!/usr/bin/env bash
# Asks the broker whether a given application version is safe to deploy.
#
# Safe means: every contract this version is party to has been verified by the
# other side. A provider that has not yet verified a consumer's latest pact is
# not a warning here, it is a failure.
set -euo pipefail

APP="${1:?usage: can-i-deploy.sh <application> [version]}"
VERSION="${2:-$(git rev-parse --short HEAD)}"
BROKER_URL="${PACT_BROKER_BASE_URL:-http://host.docker.internal:9292}"
BROKER_USER="${PACT_BROKER_USERNAME:-pact}"
BROKER_PASS="${PACT_BROKER_PASSWORD:-pact}"

# MSYS_NO_PATHCONV stops Git Bash on Windows rewriting container paths like /pacts
# into host paths. It is ignored everywhere else.
MSYS_NO_PATHCONV=1 docker run --rm \
  --add-host host.docker.internal:host-gateway \
  pactfoundation/pact-cli:latest \
  broker can-i-deploy \
    --broker-base-url "${BROKER_URL}" \
    --broker-username "${BROKER_USER}" \
    --broker-password "${BROKER_PASS}" \
    --pacticipant "${APP}" \
    --version "${VERSION}"
