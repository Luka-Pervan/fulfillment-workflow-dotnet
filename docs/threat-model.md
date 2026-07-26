# Threat model

## Assets

- fulfillment state and audit-relevant timestamps;
- authorization boundaries between operators and supervisors;
- database and token-signing credentials;
- outbox integrity and event delivery state;
- service availability.

The sample intentionally stores no personal data.

## Trust boundaries

1. client to HTTPS API;
2. API to identity provider;
3. API/worker to database;
4. worker to an event transport;
5. service to telemetry backend.

Local Docker traffic is not a production security boundary.

## Primary threats and controls

| Threat | Control in sample | Production follow-up |
|---|---|---|
| Unauthorized state change | JWT role policies; transition requires Supervisor | External IdP, least-privilege scopes, access review |
| Replay or duplicate write | Key bound to normalized request fingerprint; unique index | Retention and abuse policy |
| Lost integration event | Aggregate and outbox intent share one commit; bounded demonstration retry | Durable broker, alerting, backoff, dead-letter flow |
| Duplicate integration event | Stable event ID, at-least-once contract | Consumer deduplication |
| Lost update | Expected version and EF concurrency token | ETag/If-Match contract if clients prefer HTTP preconditions |
| Secret disclosure | No tracked secrets; environment configuration | Managed identity/secret store and rotation |
| Sensitive telemetry | Payloads are not logged | Attribute allow-list and retention policy |
| Resource exhaustion | Subject-partitioned fixed window, IP fallback, `Retry-After`, paging cap | Distributed limiter, trusted-proxy configuration, quotas, load tests |

## Development token issuer

`/dev/token` is mapped only in `Development`. It is a convenience for local
evaluation, not an identity system. Production must omit the signing key and
configure a trusted OpenID Connect authority and audience.

## Known residual risks

- The in-memory limiter is not a distributed abuse control.
- IP fallback uses the connection address. A reverse-proxy deployment must
  configure forwarded headers with explicit trusted proxies before relying on
  client-IP partitions.
- Concurrent identical first-use requests resolve to one create and one
  replay. A concurrent request with the same key but different payload returns
  `409` after validating the committed winner.
- The demonstration publisher needs a row-claiming strategy before multiple
  replicas are deployed.
- Failed demonstration deliveries retain only the exception type. Ten failed
  attempts stop automatic selection; a real deployment needs an alert and
  operator recovery path.
## Browser console considerations

- Development bearer tokens live in React memory only. They are never written
  to local storage, session storage, cookies, source code, or logs.
- `/dev/token` remains Development-only. Static assets can be served in any
  environment, but production authentication requires the configured external
  authority and a separately reviewed browser sign-in design.
- The UI and API are served from one origin in the container, avoiding a broad
  CORS policy. Vite proxies the same paths only for local development.
- Responses set a restrictive Content Security Policy, disable framing and MIME
  sniffing, and suppress referrer data.
- The frontend does not make authorization decisions. Operator transition
  controls are hidden for clarity, while the API policy remains the security
  boundary and returns `403` for unauthorized calls.
- Displayed values use React text rendering; there is no raw HTML injection.
- All bundled examples and browser tests use synthetic identifiers and
  destinations.
