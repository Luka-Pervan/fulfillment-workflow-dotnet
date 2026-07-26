# ADR-001: Keep domain and use cases independent of hosting

**Status:** Accepted

## Context

The sample must show business rules and delivery concerns without making the
domain depend on ASP.NET Core or EF Core.

## Decision

Use four projects: Domain, Application, Infrastructure, and API. Application
defines persistence and event-sink ports; Infrastructure implements them; API
composes the process.

## Consequences

Business rules are easy to test and hosting can change. The cost is more
projects and mapping code than a small CRUD API strictly requires.
