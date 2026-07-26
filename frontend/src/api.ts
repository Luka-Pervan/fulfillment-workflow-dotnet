import type {
  CreateFulfillmentInput,
  CreateFulfillmentOutcome,
  DevelopmentToken,
  FulfillmentRequest,
  FulfillmentStatus,
  PagedResult,
  ProblemDetails,
  Role
} from "./types";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly traceId?: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  token?: string
): Promise<{ data: T; status: number }> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (options.body) {
    headers.set("Content-Type", "application/json");
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(path, { ...options, headers });

  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      problem = undefined;
    }

    throw new ApiError(
      response.status,
      problem?.detail ??
        problem?.title ??
        `The service returned HTTP ${response.status}.`,
      problem?.traceId
    );
  }

  return {
    data: (await response.json()) as T,
    status: response.status
  };
}

export async function createDevelopmentToken(
  role: Role
): Promise<DevelopmentToken> {
  return (
    await request<DevelopmentToken>("/dev/token", {
      method: "POST",
      body: JSON.stringify({ role })
    })
  ).data;
}

export async function listFulfillmentRequests(
  token: string,
  status: FulfillmentStatus | "",
  page: number,
  pageSize: number,
  signal?: AbortSignal
): Promise<PagedResult<FulfillmentRequest>> {
  const parameters = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize)
  });

  if (status) {
    parameters.set("status", status);
  }

  return (
    await request<PagedResult<FulfillmentRequest>>(
      `/api/fulfillment-requests?${parameters.toString()}`,
      { signal },
      token
    )
  ).data;
}

export async function getFulfillmentRequest(
  token: string,
  id: string
): Promise<FulfillmentRequest> {
  return (
    await request<FulfillmentRequest>(
      `/api/fulfillment-requests/${encodeURIComponent(id)}`,
      {},
      token
    )
  ).data;
}

export async function createFulfillmentRequest(
  token: string,
  input: CreateFulfillmentInput,
  idempotencyKey: string
): Promise<CreateFulfillmentOutcome> {
  const response = await request<FulfillmentRequest>(
    "/api/fulfillment-requests",
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(input)
    },
    token
  );

  return {
    request: response.data,
    replayed: response.status === 200
  };
}

export async function transitionFulfillmentRequest(
  token: string,
  id: string,
  status: FulfillmentStatus,
  expectedVersion: number
): Promise<FulfillmentRequest> {
  return (
    await request<FulfillmentRequest>(
      `/api/fulfillment-requests/${encodeURIComponent(id)}/transitions`,
      {
        method: "POST",
        body: JSON.stringify({ status, expectedVersion })
      },
      token
    )
  ).data;
}
