# ADR-005: Policy authorization with external production identity

**Status:** Accepted

## Context

The sample should demonstrate authorization without building an unsafe custom
password store.

## Decision

Use JWT bearer authentication and named role policies. Production
configuration expects an external authority. Development may use a
short-lived, symmetric token from an endpoint that is absent outside the
Development environment.

## Consequences

Authorization behavior is realistic and independently testable. The local
issuer is convenient but explicitly not a production identity solution.
