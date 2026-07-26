import { useCallback, useEffect, useRef, useState } from "react";
import {
  ApiError,
  createDevelopmentToken,
  createFulfillmentRequest,
  getFulfillmentRequest,
  listFulfillmentRequests,
  transitionFulfillmentRequest
} from "./api";
import {
  CreateRequestForm,
  Notice,
  RequestDetails,
  RequestDetailsPlaceholder,
  RequestList,
  RoleSwitcher,
  describeApiError
} from "./components";
import type {
  CreateFulfillmentInput,
  CreateFulfillmentOutcome,
  FulfillmentRequest,
  FulfillmentStatus,
  PagedResult,
  Role
} from "./types";

const pageSize = 8;

export function App() {
  const [role, setRole] = useState<Role | null>(null);
  const [loadingRole, setLoadingRole] = useState<Role | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<FulfillmentStatus | "">("");
  const [page, setPage] = useState(1);
  const [result, setResult] =
    useState<PagedResult<FulfillmentRequest> | null>(null);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [selected, setSelected] = useState<FulfillmentRequest | null>(null);
  const [transitioning, setTransitioning] =
    useState<FulfillmentStatus | null>(null);
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [refreshSequence, setRefreshSequence] = useState(0);
  const detailsRegionRef = useRef<HTMLDivElement>(null);

  const loadRequests = useCallback(
    async (signal?: AbortSignal) => {
      if (!token) {
        return;
      }

      setListLoading(true);
      setListError(null);
      try {
        const nextResult = await listFulfillmentRequests(
          token,
          statusFilter,
          page,
          pageSize,
          signal
        );
        setResult(nextResult);

        if (page > 1 && nextResult.items.length === 0) {
          setPage(page - 1);
        }
      } catch (caught) {
        if (caught instanceof DOMException && caught.name === "AbortError") {
          return;
        }
        setListError(describeApiError(caught));
      } finally {
        if (!signal?.aborted) {
          setListLoading(false);
        }
      }
    },
    [page, statusFilter, token]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadRequests(controller.signal);
    return () => controller.abort();
  }, [loadRequests, refreshSequence]);

  useEffect(() => {
    if (!selected) {
      return;
    }

    const detailsRegion = detailsRegionRef.current;
    const mobileLayout = window.matchMedia("(max-width: 840px)").matches;
    detailsRegion?.focus({ preventScroll: !mobileLayout });
    if (mobileLayout) {
      detailsRegion?.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [selected?.id]);

  const chooseRole = async (nextRole: Role) => {
    setLoadingRole(nextRole);
    setSessionError(null);
    try {
      const developmentToken = await createDevelopmentToken(nextRole);
      setRole(nextRole);
      setToken(developmentToken.accessToken);
      setExpiresAt(developmentToken.expiresAt);
      setPage(1);
      setResult(null);
      setListError(null);
      setSelected(null);
      setTransitionError(null);
    } catch (caught) {
      setSessionError(describeApiError(caught));
    } finally {
      setLoadingRole(null);
    }
  };

  const createRequest = async (
    input: CreateFulfillmentInput,
    idempotencyKey: string
  ): Promise<CreateFulfillmentOutcome> => {
    if (!token) {
      throw new ApiError(401, "Choose a development role first.");
    }

    const outcome = await createFulfillmentRequest(
      token,
      input,
      idempotencyKey
    );
    setSelected(outcome.request);
    setPage(1);
    setRefreshSequence((value) => value + 1);
    return outcome;
  };

  const refreshSelected = async () => {
    if (!token || !selected) {
      return;
    }

    try {
      setSelected(await getFulfillmentRequest(token, selected.id));
    } catch (caught) {
      setTransitionError(describeApiError(caught));
    }
  };

  const transitionRequest = async (status: FulfillmentStatus) => {
    if (!token || !selected) {
      return;
    }

    setTransitioning(status);
    setTransitionError(null);
    try {
      const updated = await transitionFulfillmentRequest(
        token,
        selected.id,
        status,
        selected.version
      );
      setSelected(updated);
      setRefreshSequence((value) => value + 1);
    } catch (caught) {
      setTransitionError(describeApiError(caught));
      if (caught instanceof ApiError && caught.status === 409) {
        await refreshSelected();
        setRefreshSequence((value) => value + 1);
      }
    } finally {
      setTransitioning(null);
    }
  };

  return (
    <>
      <header className="app-header">
        <div className="header-inner">
          <div className="brand-mark" aria-hidden="true">
            FO
          </div>
          <div className="product-name">
            <span>Operations</span>
            <h1>Fulfillment console</h1>
          </div>
          <div
            className={`service-state ${
              token ? "is-connected" : sessionError ? "is-error" : "is-idle"
            }`}
            role="status"
          >
            <span className="service-state-dot" aria-hidden="true" />
            <span>
              <strong>API</strong>
              {token
                ? "Connected"
                : sessionError
                  ? "Unavailable"
                  : "Not checked"}
            </span>
          </div>
          <RoleSwitcher
            disabled={transitioning !== null}
            expiresAt={expiresAt}
            loadingRole={loadingRole}
            onSelect={chooseRole}
            role={role}
          />
        </div>
      </header>

      <main>
        <section className="operations-summary" aria-labelledby="page-heading">
          <div>
            <p className="breadcrumb">Operations / Fulfillment</p>
            <h2 id="page-heading">Fulfillment requests</h2>
            <p>
              Create work, inspect its current version and apply only valid
              state transitions.
            </p>
          </div>
          <ol className="workflow-key" aria-label="Fulfillment workflow paths">
            <li>
              <span>01</span> Submitted
            </li>
            <li>
              <span>02</span> Processing
            </li>
            <li>
              <span>03</span> Completed
            </li>
            <li className="workflow-exit">
              <span>↳</span> Cancelled
              <small>from open states</small>
            </li>
          </ol>
        </section>

        {sessionError ? (
          <Notice role="alert" tone="error">
            {sessionError}
          </Notice>
        ) : null}

        <div className="workbench">
          <div className="queue-workspace">
            <CreateRequestForm
              disabled={!token || transitioning !== null}
              onCreate={createRequest}
            />
            <RequestList
              error={listError}
              loading={listLoading}
              onFilterChange={(status) => {
                setStatusFilter(status);
                setPage(1);
              }}
              onPageChange={setPage}
              onRefresh={async () =>
                setRefreshSequence((value) => value + 1)
              }
              onSelect={(request) => {
                setSelected(request);
                setTransitionError(null);
              }}
              result={result}
              selectedId={selected?.id ?? null}
              selectionDisabled={transitioning !== null}
              statusFilter={statusFilter}
            />
          </div>

          <div
            className="details-region"
            ref={detailsRegionRef}
            tabIndex={-1}
          >
            {selected && role ? (
              <RequestDetails
                onClose={() => setSelected(null)}
                onTransition={transitionRequest}
                request={selected}
                role={role}
                transitionError={transitionError}
                transitioning={transitioning}
              />
            ) : (
              <RequestDetailsPlaceholder hasSession={token !== null} />
            )}
          </div>
        </div>
      </main>

      <footer>
        Original educational project · Synthetic data only · No employer code
        or confidential information
      </footer>
    </>
  );
}
