# Testing strategy

## Test pyramid

- Domain tests exhaust the important state-machine paths and confirm that
  failed transitions do not mutate the aggregate.
- HTTP integration tests run the complete ASP.NET Core pipeline with policy
  authorization, Problem Details, EF Core, migrations, and SQLite in memory.
- The PostgreSQL gate applies migrations explicitly, runs provider-level
  persistence assertions, starts the release build, and waits for readiness.

## Why two providers

SQLite makes the default suite fast and reliable when Docker is unavailable.
It is not treated as proof of PostgreSQL parity. CI therefore runs an opt-in
test against a real PostgreSQL service and verifies from TRX output that the
test actually executed.

Provider-sensitive query behavior or concurrency semantics should gain a
PostgreSQL integration test before being relied on.

## Important scenarios

- anonymous request returns `401`;
- Operator is forbidden from transitions;
- create and normalized sequential replay return one aggregate;
- concurrent identical PostgreSQL creates resolve to create plus replay with
  one aggregate and one outbox message;
- the same idempotency key with a changed payload returns `409`;
- a row created before the request-hash migration still replays safely;
- create persists an outbox record;
- Supervisor can transition;
- stale expected version returns `409`;
- invalid input returns `application/problem+json`;
- explicit migration succeeds on an empty PostgreSQL database;
- PostgreSQL enforces the unique key and optimistic concurrency token;
- aggregate and outbox rows are visible after the same application commit;
- a failed demonstration dispatch records only an exception type and a later
  invocation retries and marks the message published;
- subject-partitioned throttling returns `Retry-After` and Problem Details.

## Coverage

Coverage is evidence, not a target to game. Domain and application branches
should remain well covered, while generated migration and hosting lines need
not be padded with low-value tests.
## Frontend verification

Vitest and Testing Library cover the observable browser contracts: stable
idempotency keys, `201` versus `200` replay semantics, filtering, paging,
role-sensitive actions, and explicit error language for the important HTTP
failure classes.

Playwright starts the release API against a fresh temporary SQLite database and
the Vite development server. The Chromium flow acquires a real Development
token, creates and replays a synthetic request, advances it through valid
Supervisor transitions, and runs axe against the resulting screen. This keeps
the browser test integrated with the real authorization, persistence,
concurrency, and state-machine paths rather than mocking them.

## OpenAPI drift decision

The Development host exposes the built-in OpenAPI document. The small
TypeScript client is intentionally handwritten and its behavior is covered by
HTTP integration tests plus a real-backend browser flow. A committed generated
client or OpenAPI snapshot is deferred until it can replace a manual contract,
because adding a second generated artifact now would increase maintenance
without catching more than the existing end-to-end gate.
