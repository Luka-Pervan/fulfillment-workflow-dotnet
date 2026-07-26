# Architecture

## Boundaries

- **Domain** owns the aggregate, state machine, invariants, and outbox message
  state. It has no package references.
- **Application** owns use cases and dependency ports. It coordinates an
  aggregate and its outbox event in one unit of work.
- **Infrastructure** owns EF Core mappings, migrations, repository behavior,
  the outbox worker, and the demonstration event sink.
- **API** owns HTTP contracts, JWT configuration, authorization policies,
  Problem Details, health checks, rate limiting, correlation, telemetry, and
  dependency composition.

## Write path

1. Authentication establishes a role.
2. Authorization checks the endpoint policy.
3. The create endpoint validates the request and `Idempotency-Key`.
4. The application service hashes the normalized semantic fields and checks
   for a prior request with that key.
5. A matching key and fingerprint replays; a different fingerprint fails with
   a clear conflict.
6. A new aggregate and outbox message are added to one `DbContext`.
7. One `SaveChanges` commits both records.
8. The demonstration dispatcher retries pending messages up to a bounded
   attempt limit and marks successful delivery.

The logging sink deliberately exposes only event ID and type. It does not log
the payload.

## Consistency

The unique idempotency index is the final database guard. Sequential retries
return the stored response only when their canonical request fingerprints
match. Reusing a key for different semantics fails closed. If two requests race
before either commits, one wins; the loser reads and validates that committed
winner, then returns a replay for the same payload or `409` for a mismatch.

State changes include an expected aggregate version. EF also marks the version
column as a concurrency token, protecting both the API precondition and the
final update.

The demonstration dispatcher has at-least-once behavior, so consumers must
deduplicate by event ID. It stores a bounded exception type rather than a raw
exception message after a failed attempt. A real transport still needs backoff,
dead-letter monitoring, and an operational recovery path.

## Schema deployment

The normal production API process does not mutate its schema. The same release
artifact exposes `--migrate` as an explicit, exit-after-completion deployment
command. Compose and CI run that command before starting the API. Development
and Testing may opt into startup migration for a short local feedback loop;
the flag is rejected in every other environment.

## Scaling boundary

The current publisher is intentionally single-instance. A horizontally scaled
deployment should atomically claim rows, for example with PostgreSQL
`FOR UPDATE SKIP LOCKED`, use a lease, or delegate dispatch to infrastructure
that supplies equivalent semantics.
## Frontend boundary

The `frontend` package is a delivery adapter, not another domain layer. It owns
browser interaction state, typed HTTP contracts, accessibility, and display
logic. The API remains authoritative for authorization, validation, allowed
state transitions, concurrency, and idempotency.

Development uses a Vite proxy so browser requests remain same-origin from the
application's perspective. The container builds hashed frontend assets in a
separate Node stage and copies only `dist` into the non-root ASP.NET runtime
image. ASP.NET Core serves those assets and applies the same security headers
to UI and API responses.

The console does mirror the set of status labels to render controls, but a
transition is never assumed successful until the API accepts the supplied
`expectedVersion`. HTTP `409` triggers a fresh detail read; HTTP `422` remains a
business-rule rejection and is shown as such.
