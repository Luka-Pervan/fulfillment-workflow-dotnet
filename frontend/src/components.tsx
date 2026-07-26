import {
  type FormEvent,
  type ReactNode,
  useId,
  useMemo,
  useState
} from "react";
import { ApiError } from "./api";
import { createIdempotencyKey } from "./idempotency";
import { getAllowedTransitions } from "./transitions";
import {
  fulfillmentStatuses,
  type CreateFulfillmentInput,
  type CreateFulfillmentOutcome,
  type FulfillmentRequest,
  type FulfillmentStatus,
  type PagedResult,
  type Role
} from "./types";

export function StatusBadge({ status }: { status: FulfillmentStatus }) {
  return (
    <span className={`status-badge status-${status.toLowerCase()}`}>
      <span aria-hidden="true" className="status-dot" />
      {status}
    </span>
  );
}

export function Notice({
  children,
  tone = "info",
  role
}: {
  children: ReactNode;
  tone?: "info" | "success" | "warning" | "error";
  role?: "alert" | "status";
}) {
  return (
    <div className={`notice notice-${tone}`} role={role}>
      {children}
    </div>
  );
}

export function RoleSwitcher({
  role,
  loadingRole,
  expiresAt,
  disabled,
  onSelect
}: {
  role: Role | null;
  loadingRole: Role | null;
  expiresAt: string | null;
  disabled: boolean;
  onSelect: (role: Role) => Promise<void>;
}) {
  return (
    <section className="identity-control" aria-labelledby="role-heading">
      <h2 className="sr-only" id="role-heading">
        Development identity
      </h2>
      <span className="identity-label">Identity</span>
      <div className="role-actions" aria-label="Development role">
        {(["Operator", "Supervisor"] as const).map((candidate) => (
          <button
            aria-pressed={role === candidate}
            className={role === candidate ? "role-button is-active" : "role-button"}
            disabled={disabled || loadingRole !== null}
            key={candidate}
            onClick={() => void onSelect(candidate)}
            type="button"
          >
            {loadingRole === candidate ? "Connecting…" : candidate}
          </button>
        ))}
      </div>
      {role && expiresAt ? (
        <p className="session-copy" role="status">
          Active as <strong>{role}</strong> · expires{" "}
          {new Intl.DateTimeFormat(undefined, {
            hour: "2-digit",
            minute: "2-digit"
          }).format(new Date(expiresAt))}
        </p>
      ) : (
        <p className="session-copy">Select a role to connect</p>
      )}
    </section>
  );
}

export function CreateRequestForm({
  disabled,
  onCreate
}: {
  disabled: boolean;
  onCreate: (
    input: CreateFulfillmentInput,
    idempotencyKey: string
  ) => Promise<CreateFulfillmentOutcome>;
}) {
  const externalReferenceId = useId();
  const countryId = useId();
  const postalCodeId = useId();
  const [externalReference, setExternalReference] = useState("");
  const [countryCode, setCountryCode] = useState("HR");
  const [postalCode, setPostalCode] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState(createIdempotencyKey);
  const [outcome, setOutcome] = useState<CreateFulfillmentOutcome | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const startNewRequest = () => {
    setExternalReference("");
    setCountryCode("HR");
    setPostalCode("");
    setIdempotencyKey(createIdempotencyKey());
    setOutcome(null);
    setError(null);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      const nextOutcome = await onCreate(
        {
          externalReference,
          destinationCountryCode: countryCode,
          destinationPostalCode: postalCode
        },
        idempotencyKey
      );
      setOutcome(nextOutcome);
    } catch (caught) {
      setError(describeApiError(caught));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="card create-card" aria-labelledby="create-heading">
      <div className="section-heading">
        <div>
          <p className="section-code">NEW REQUEST</p>
          <h2 id="create-heading">Create fulfillment work</h2>
          <p className="supporting-copy">
            One command, one stable idempotency key.
          </p>
        </div>
        {outcome ? (
          <button
            className="button-quiet"
            onClick={startNewRequest}
            type="button"
          >
            Start new request
          </button>
        ) : null}
      </div>

      <form onSubmit={(event) => void handleSubmit(event)}>
        <div className="form-grid">
          <label htmlFor={externalReferenceId}>
            External reference
            <input
              autoComplete="off"
              disabled={disabled || submitting || outcome !== null}
              id={externalReferenceId}
              maxLength={100}
              onChange={(event) => setExternalReference(event.target.value)}
              placeholder="ORDER-2026-0042"
              required
              value={externalReference}
            />
          </label>
          <label htmlFor={countryId}>
            Country
            <input
              autoCapitalize="characters"
              autoComplete="country"
              disabled={disabled || submitting || outcome !== null}
              id={countryId}
              maxLength={2}
              minLength={2}
              onChange={(event) =>
                setCountryCode(event.target.value.toUpperCase())
              }
              pattern="[A-Za-z]{2}"
              required
              value={countryCode}
            />
          </label>
          <label htmlFor={postalCodeId}>
            Postal code
            <input
              autoComplete="postal-code"
              disabled={disabled || submitting || outcome !== null}
              id={postalCodeId}
              maxLength={20}
              onChange={(event) => setPostalCode(event.target.value)}
              placeholder="21000"
              required
              value={postalCode}
            />
          </label>
        </div>

        <div className="idempotency-box">
          <span>Idempotency key</span>
          <code>{idempotencyKey}</code>
          <p>
            This key remains stable across retries. After a successful create,
            submit once more to observe the API’s replay response.
          </p>
        </div>

        {error ? (
          <Notice role="alert" tone="error">
            {error}
          </Notice>
        ) : null}
        {outcome ? (
          <Notice role="status" tone={outcome.replayed ? "info" : "success"}>
            <strong>
              {outcome.replayed
                ? "Existing request replayed (HTTP 200)."
                : "New request created (HTTP 201)."}
            </strong>{" "}
            Request {outcome.request.externalReference} is version{" "}
            {outcome.request.version}.
          </Notice>
        ) : null}

        <button
          className="button-primary submit-button"
          disabled={disabled || submitting}
          type="submit"
        >
          {submitting
            ? "Submitting…"
            : outcome
              ? "Submit again with same key"
              : "Create request"}
        </button>
      </form>
    </section>
  );
}

export function RequestList({
  result,
  loading,
  error,
  selectedId,
  statusFilter,
  onFilterChange,
  onPageChange,
  onRefresh,
  onSelect,
  selectionDisabled
}: {
  result: PagedResult<FulfillmentRequest> | null;
  loading: boolean;
  error: string | null;
  selectedId: string | null;
  statusFilter: FulfillmentStatus | "";
  onFilterChange: (status: FulfillmentStatus | "") => void;
  onPageChange: (page: number) => void;
  onRefresh: () => Promise<void>;
  onSelect: (request: FulfillmentRequest) => void;
  selectionDisabled: boolean;
}) {
  const totalPages = result
    ? Math.max(1, Math.ceil(result.total / result.pageSize))
    : 1;

  return (
    <section className="card list-card" aria-labelledby="queue-heading">
      <div className="section-heading list-heading">
        <div>
          <p className="section-code">LIVE QUEUE</p>
          <h2 id="queue-heading">Requests</h2>
          <p className="supporting-copy">
            {result
              ? `${result.total} matching ${result.total === 1 ? "item" : "items"}`
              : "Awaiting a development session"}
          </p>
        </div>
        <div className="list-controls">
          <label>
            <span className="sr-only">Filter by status</span>
            <select
              onChange={(event) =>
                onFilterChange(event.target.value as FulfillmentStatus | "")
              }
              value={statusFilter}
            >
              <option value="">All statuses</option>
              {fulfillmentStatuses.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </label>
          <button
            className="button-secondary"
            disabled={loading}
            onClick={() => void onRefresh()}
            type="button"
          >
            {loading ? "Refreshing…" : "Refresh"}
          </button>
        </div>
      </div>

      {error ? (
        <Notice role="alert" tone="error">
          {error}
        </Notice>
      ) : null}
      {loading && !result ? (
        <div className="state-panel" role="status">
          <span className="spinner" aria-hidden="true" />
          Loading request queue…
        </div>
      ) : null}
      {!loading && result?.items.length === 0 ? (
        <div className="state-panel">
          <strong>No matching requests</strong>
          <span>Create a request or choose another status filter.</span>
        </div>
      ) : null}
      {result && result.items.length > 0 ? (
        <div className="table-scroll">
          <table>
            <caption className="sr-only">
              Fulfillment requests, page {result.page} of {totalPages}
            </caption>
            <thead>
              <tr>
                <th scope="col">Reference</th>
                <th scope="col">Destination</th>
                <th scope="col">Status</th>
                <th scope="col">Updated</th>
                <th scope="col">
                  <span className="sr-only">Action</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((request) => (
                <tr
                  className={selectedId === request.id ? "selected-row" : ""}
                  key={request.id}
                >
                  <td>
                    <strong>{request.externalReference}</strong>
                    <span className="cell-meta">v{request.version}</span>
                  </td>
                  <td>
                    {request.destinationCountryCode} ·{" "}
                    {request.destinationPostalCode}
                  </td>
                  <td>
                    <StatusBadge status={request.status} />
                  </td>
                  <td>{formatRelativeDate(request.updatedAt)}</td>
                  <td>
                    <button
                      aria-current={
                        selectedId === request.id ? "true" : undefined
                      }
                      className="button-quiet"
                      disabled={selectionDisabled}
                      onClick={() => onSelect(request)}
                      type="button"
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {result && result.total > 0 ? (
        <nav className="pagination" aria-label="Request pages">
          <button
            className="button-secondary"
            disabled={loading || result.page <= 1}
            onClick={() => onPageChange(result.page - 1)}
            type="button"
          >
            Previous
          </button>
          <span>
            Page <strong>{result.page}</strong> of {totalPages} · {result.total}{" "}
            total
          </span>
          <button
            className="button-secondary"
            disabled={loading || result.page >= totalPages}
            onClick={() => onPageChange(result.page + 1)}
            type="button"
          >
            Next
          </button>
        </nav>
      ) : null}
    </section>
  );
}

export function RequestDetails({
  request,
  role,
  transitioning,
  transitionError,
  onTransition,
  onClose
}: {
  request: FulfillmentRequest;
  role: Role;
  transitioning: FulfillmentStatus | null;
  transitionError: string | null;
  onTransition: (status: FulfillmentStatus) => Promise<void>;
  onClose: () => void;
}) {
  const allowedTransitions = useMemo(
    () => getAllowedTransitions(request.status),
    [request.status]
  );

  return (
    <aside className="card details-card" aria-labelledby="details-heading">
      <div className="section-heading">
        <div>
          <p className="section-code">REQUEST DETAIL</p>
          <h2 id="details-heading">{request.externalReference}</h2>
        </div>
        <button
          className="button-quiet"
          disabled={transitioning !== null}
          onClick={onClose}
          type="button"
        >
          Close
        </button>
      </div>

      <WorkflowProgress status={request.status} />

      <dl className="details-grid">
        <div>
          <dt>Status</dt>
          <dd>
            <StatusBadge status={request.status} />
          </dd>
        </div>
        <div>
          <dt>Version</dt>
          <dd>{request.version}</dd>
        </div>
        <div>
          <dt>Destination</dt>
          <dd>
            {request.destinationCountryCode} ·{" "}
            {request.destinationPostalCode}
          </dd>
        </div>
        <div>
          <dt>Updated</dt>
          <dd>{formatDate(request.updatedAt)}</dd>
        </div>
        <div className="details-id">
          <dt>Request ID</dt>
          <dd>
            <code>{request.id}</code>
          </dd>
        </div>
      </dl>

      <div className="transition-panel">
        <h3>Workflow actions</h3>
        {role !== "Supervisor" ? (
          <Notice tone="warning">
            Operator can inspect and create requests. Switch to Supervisor to
            change workflow state.
          </Notice>
        ) : allowedTransitions.length === 0 ? (
          <p className="supporting-copy">
            This request is in a terminal state. No transitions are available.
          </p>
        ) : (
          <div className="transition-actions">
            {allowedTransitions.map((status) => (
              <button
                className={
                  status === "Cancelled"
                    ? "button-danger"
                    : "button-primary"
                }
                disabled={transitioning !== null}
                key={status}
                onClick={() => void onTransition(status)}
                type="button"
              >
                {transitioning === status ? "Applying…" : `Move to ${status}`}
              </button>
            ))}
          </div>
        )}
        <p className="supporting-copy">
          The API receives expectedVersion {request.version}; stale writes
          return HTTP 409 instead of silently overwriting another update.
        </p>
        {transitionError ? (
          <Notice role="alert" tone="error">
            {transitionError}
          </Notice>
        ) : null}
      </div>
    </aside>
  );
}

export function RequestDetailsPlaceholder({
  hasSession
}: {
  hasSession: boolean;
}) {
  return (
    <aside
      className="card details-card details-placeholder"
      aria-labelledby="details-placeholder-heading"
    >
      <div className="placeholder-graphic" aria-hidden="true">
        <span />
        <span />
        <span />
      </div>
      <p className="section-code">REQUEST DETAIL</p>
      <h2 id="details-placeholder-heading">
        {hasSession ? "Select a request" : "Connect an identity"}
      </h2>
      <p>
        {hasSession
          ? "Open an item from the queue to inspect its version, destination and available workflow actions."
          : "Choose Operator or Supervisor in the top bar to load the synthetic fulfillment queue."}
      </p>
    </aside>
  );
}

function WorkflowProgress({ status }: { status: FulfillmentStatus }) {
  const primaryPath: FulfillmentStatus[] = [
    "Submitted",
    "Processing",
    "Completed"
  ];
  const activeIndex = primaryPath.indexOf(status);

  return (
    <div className="workflow-progress">
      <p className="workflow-progress-label">Workflow</p>
      <ol aria-label={`Current workflow status: ${status}`}>
        {primaryPath.map((step, index) => {
          const reached = status !== "Cancelled" && index <= activeIndex;
          const current = step === status;
          return (
            <li
              className={`${reached ? "is-reached" : ""} ${
                current ? "is-current" : ""
              }`}
              key={step}
            >
              <span aria-hidden="true">{reached ? "✓" : index + 1}</span>
              <strong>{step}</strong>
              {current ? <small>Current</small> : null}
            </li>
          );
        })}
      </ol>
      {status === "Cancelled" ? (
        <p className="cancelled-state">
          <span aria-hidden="true">×</span>
          Request cancelled
        </p>
      ) : null}
    </div>
  );
}

export function describeApiError(caught: unknown): string {
  if (!(caught instanceof ApiError)) {
    return "The service could not be reached. Confirm that the API is running.";
  }

  const prefix = (() => {
    switch (caught.status) {
      case 401:
        return "Your development session is missing or expired.";
      case 403:
        return "This role is not allowed to perform that action.";
      case 409:
        return "The request changed before this action completed.";
      case 422:
        return "The requested workflow transition is not valid.";
      case 429:
        return "The request rate limit was reached. Wait briefly and retry.";
      default:
        return `The service rejected the request (HTTP ${caught.status}).`;
    }
  })();

  return `${prefix} ${caught.message}${
    caught.traceId ? ` Trace: ${caught.traceId}` : ""
  }`;
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function formatRelativeDate(value: string): string {
  const date = new Date(value);
  const seconds = Math.round((date.getTime() - Date.now()) / 1000);
  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

  if (Math.abs(seconds) < 60) {
    return formatter.format(seconds, "second");
  }

  const minutes = Math.round(seconds / 60);
  if (Math.abs(minutes) < 60) {
    return formatter.format(minutes, "minute");
  }

  const hours = Math.round(minutes / 60);
  if (Math.abs(hours) < 24) {
    return formatter.format(hours, "hour");
  }

  return formatDate(value);
}
