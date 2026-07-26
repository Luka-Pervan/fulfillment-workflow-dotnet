# ADR-004: Use a transactional outbox

**Status:** Accepted

## Context

Updating workflow state and publishing an integration event as two unrelated
operations can lose an event or publish an event for a rolled-back write.

## Decision

Store the aggregate and an outbox message in one EF Core unit of work. A
background worker invokes a bounded demonstration dispatcher, which publishes
pending messages, records an exception type on failure, and marks successful
delivery.

## Consequences

Database and event intent remain consistent. Demonstration delivery is
at-least-once, so consumers must deduplicate. It stops selecting a message after
ten failed attempts; production use requires backoff, dead-letter monitoring,
and operator recovery. Multiple worker replicas require row claiming or a
lease, which this single-instance sample documents but does not pretend to
solve.
