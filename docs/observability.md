# Observability

The API emits ASP.NET Core traces and metrics plus .NET runtime metrics through
OpenTelemetry. Export is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is
configured.

The registered `Fulfillment.Api` meter adds only bounded operational signals:
create outcomes (`created` or `replayed`), idempotency payload conflicts, and
rate-limit rejections. It does not attach request keys, subject IDs, IP
addresses, external references, payloads, or other high-cardinality values.

The compose stack sends OTLP/gRPC to a collector whose debug exporter prints
summaries. This proves instrumentation and propagation without selecting a
vendor.

Every response receives `X-Correlation-ID`. A valid inbound value is retained;
otherwise the active trace ID or a generated ID is used.

Production follow-up should add:

- explicit service version and environment resource attributes;
- transition outcomes and outbox age/backlog metrics;
- an outbox backlog/failed-attempt alert;
- latency and error SLOs;
- trace sampling and attribute allow-list policies;
- a durable metrics, logs, and traces backend.
