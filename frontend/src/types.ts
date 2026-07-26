export const fulfillmentStatuses = [
  "Submitted",
  "Processing",
  "Completed",
  "Cancelled"
] as const;

export type FulfillmentStatus = (typeof fulfillmentStatuses)[number];
export type Role = "Operator" | "Supervisor";

export interface FulfillmentRequest {
  id: string;
  externalReference: string;
  destinationCountryCode: string;
  destinationPostalCode: string;
  status: FulfillmentStatus;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface CreateFulfillmentInput {
  externalReference: string;
  destinationCountryCode: string;
  destinationPostalCode: string;
}

export interface CreateFulfillmentOutcome {
  request: FulfillmentRequest;
  replayed: boolean;
}

export interface DevelopmentToken {
  accessToken: string;
  expiresAt: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
}
