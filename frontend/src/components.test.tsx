import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "./api";
import {
  CreateRequestForm,
  RequestDetails,
  RequestList,
  describeApiError
} from "./components";
import type { FulfillmentRequest } from "./types";

const request: FulfillmentRequest = {
  id: "a810b7ad-8bb0-4b5f-a653-2d835057c15a",
  externalReference: "ORDER-42",
  destinationCountryCode: "HR",
  destinationPostalCode: "21000",
  status: "Submitted",
  createdAt: "2026-07-26T10:00:00Z",
  updatedAt: "2026-07-26T10:00:00Z",
  version: 1
};

describe("CreateRequestForm", () => {
  it("keeps the same idempotency key for a deliberate replay", async () => {
    const user = userEvent.setup();
    const keys: string[] = [];
    const onCreate = vi
      .fn()
      .mockImplementation(
        (_input: unknown, key: string) => {
          keys.push(key);
          return Promise.resolve({
            request,
            replayed: keys.length > 1
          });
        }
      );

    render(<CreateRequestForm disabled={false} onCreate={onCreate} />);

    await user.type(screen.getByLabelText("External reference"), "ORDER-42");
    await user.clear(screen.getByLabelText("Postal code"));
    await user.type(screen.getByLabelText("Postal code"), "21000");
    await user.click(screen.getByRole("button", { name: "Create request" }));

    expect(
      await screen.findByText(/New request created \(HTTP 201\)/)
    ).toBeInTheDocument();
    expect(screen.getByLabelText("External reference")).toBeDisabled();
    expect(screen.getByLabelText("Country")).toBeDisabled();
    expect(screen.getByLabelText("Postal code")).toBeDisabled();

    await user.click(
      screen.getByRole("button", { name: "Submit again with same key" })
    );

    expect(
      await screen.findByText(/Existing request replayed \(HTTP 200\)/)
    ).toBeInTheDocument();
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBe(keys[1]);
  });
});

describe("RequestList", () => {
  it("supports status filtering, selection and page navigation", async () => {
    const user = userEvent.setup();
    const onFilterChange = vi.fn();
    const onPageChange = vi.fn();
    const onSelect = vi.fn();

    render(
      <RequestList
        error={null}
        loading={false}
        onFilterChange={onFilterChange}
        onPageChange={onPageChange}
        onRefresh={vi.fn().mockResolvedValue(undefined)}
        onSelect={onSelect}
        result={{ items: [request], page: 1, pageSize: 1, total: 2 }}
        selectedId={null}
        selectionDisabled={false}
        statusFilter=""
      />
    );

    await user.selectOptions(screen.getByLabelText("Filter by status"), "Submitted");
    await user.click(screen.getByRole("button", { name: "View" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(onFilterChange).toHaveBeenCalledWith("Submitted");
    expect(onSelect).toHaveBeenCalledWith(request);
    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});

describe("RequestDetails", () => {
  it("keeps state transitions supervisor-only", async () => {
    const { rerender } = render(
      <RequestDetails
        onClose={vi.fn()}
        onTransition={vi.fn().mockResolvedValue(undefined)}
        request={request}
        role="Operator"
        transitionError={null}
        transitioning={null}
      />
    );

    expect(
      screen.getByText(/Switch to Supervisor to change workflow state/)
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Move to Processing" })
    ).not.toBeInTheDocument();

    rerender(
      <RequestDetails
        onClose={vi.fn()}
        onTransition={vi.fn().mockResolvedValue(undefined)}
        request={request}
        role="Supervisor"
        transitionError={null}
        transitioning={null}
      />
    );

    expect(
      screen.getByRole("button", { name: "Move to Processing" })
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Move to Cancelled" })
    ).toBeInTheDocument();

    rerender(
      <RequestDetails
        onClose={vi.fn()}
        onTransition={vi.fn().mockResolvedValue(undefined)}
        request={request}
        role="Supervisor"
        transitionError={null}
        transitioning="Processing"
      />
    );

    expect(screen.getByRole("button", { name: "Close" })).toBeDisabled();
  });
});

describe("error messages", () => {
  it.each([
    [401, "session is missing or expired"],
    [403, "role is not allowed"],
    [409, "changed before this action"],
    [422, "transition is not valid"],
    [429, "rate limit was reached"]
  ])("explains HTTP %i", (status, expected) => {
    expect(describeApiError(new ApiError(status, "Details."))).toContain(
      expected
    );
  });
});
