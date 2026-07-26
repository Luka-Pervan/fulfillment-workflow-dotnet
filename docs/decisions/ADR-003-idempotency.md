# ADR-003: Persist the idempotency key with the aggregate

**Status:** Accepted

## Context

Clients may retry a create request after a timeout and must not create duplicate
work.

## Decision

Require an `Idempotency-Key`, persist it with a SHA-256 fingerprint of the
normalized semantic request fields, and enforce a unique database index. A
completed sequential retry returns the existing aggregate only when the
fingerprint matches. The same key with different semantics returns `409`.

## Consequences

The design prevents a retry from silently returning an unrelated result. The
fingerprint is not a secret and payloads are not logged. Existing rows migrated
without a historical fingerprint receive a sentinel; replay reconstructs their
fingerprint from the persisted semantic fields before deciding whether to
return the original result or conflict. Keys do not yet expire. In a concurrent
first-use race, the loser clears its failed write state, reads the committed
winner, and returns replay only when the fingerprints match.
