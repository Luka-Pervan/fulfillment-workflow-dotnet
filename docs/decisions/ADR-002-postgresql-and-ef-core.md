# ADR-002: PostgreSQL runtime with SQLite test fallback

**Status:** Accepted

## Context

Runtime behavior should use a production-capable relational database, while
the default test path must remain reliable without Docker.

## Decision

Use EF Core with PostgreSQL in compose and a required CI functional test. Use
SQLite in-memory for the HTTP suite and a SQLite file for the fastest local
run. Apply migrations through an explicit release-artifact command before
production startup.

## Consequences

The suite is fast and accessible, but SQLite is not PostgreSQL parity. The
PostgreSQL gate therefore proves the unique idempotency constraint,
optimistic-concurrency update, and aggregate-plus-outbox commit. The CI result
is parsed to fail if the provider test is not actually executed.
