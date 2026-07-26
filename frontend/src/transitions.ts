import type { FulfillmentStatus } from "./types";

export function getAllowedTransitions(
  status: FulfillmentStatus
): FulfillmentStatus[] {
  if (status === "Submitted") {
    return ["Processing", "Cancelled"];
  }

  if (status === "Processing") {
    return ["Completed", "Cancelled"];
  }

  return [];
}
