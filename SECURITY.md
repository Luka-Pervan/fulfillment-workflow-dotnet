# Security policy

This repository is an educational portfolio sample, not a hosted service.

Please report a suspected vulnerability privately through GitHub Security
Advisories after the repository is published. Do not include real credentials,
personal data, or customer data in a report.

## Supported version

Only the latest commit on `main` is supported.

## Security boundaries

- `/dev/token` is mapped only in the `Development` environment.
- The development signing key must be provided outside source control and must
  never be reused in another environment.
- A production deployment should use an external OpenID Connect provider,
  HTTPS, managed secrets, a durable event transport, and database credentials
  with minimum privileges.
- Outbox delivery is at-least-once. A real subscriber must deduplicate by event
  ID.
- The sample payload deliberately avoids personal data. Logging full request or
  event payloads is out of scope and intentionally disabled.
