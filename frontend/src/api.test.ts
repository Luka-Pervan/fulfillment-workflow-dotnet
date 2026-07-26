import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createDevelopmentToken,
  createFulfillmentRequest,
  transitionFulfillmentRequest
} from "./api";

const request = {
  id: "a810b7ad-8bb0-4b5f-a653-2d835057c15a",
  externalReference: "ORDER-42",
  destinationCountryCode: "HR",
  destinationPostalCode: "21000",
  status: "Submitted",
  createdAt: "2026-07-26T10:00:00Z",
  updatedAt: "2026-07-26T10:00:00Z",
  version: 1
};

describe("API client", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("requests a development role token without persisting it", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          accessToken: "token-value",
          expiresAt: "2026-07-26T10:30:00Z"
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const token = await createDevelopmentToken("Supervisor");

    expect(token.accessToken).toBe("token-value");
    expect(fetchMock).toHaveBeenCalledWith(
      "/dev/token",
      expect.objectContaining({ method: "POST" })
    );
  });

  it.each([
    [201, false],
    [200, true]
  ])(
    "distinguishes create HTTP %i as replayed=%s",
    async (status, replayed) => {
      const fetchMock = vi.fn().mockResolvedValue(
        new Response(JSON.stringify(request), {
          status,
          headers: { "Content-Type": "application/json" }
        })
      );
      vi.stubGlobal("fetch", fetchMock);

      const outcome = await createFulfillmentRequest(
        "token",
        {
          externalReference: "ORDER-42",
          destinationCountryCode: "HR",
          destinationPostalCode: "21000"
        },
        "console-stable-key"
      );

      expect(outcome.replayed).toBe(replayed);
      const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(new Headers(options.headers).get("Idempotency-Key")).toBe(
        "console-stable-key"
      );
    }
  );

  it("surfaces concurrency Problem Details with status and trace id", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "Concurrency conflict",
            detail: "Expected version 1 no longer matches.",
            traceId: "trace-409"
          }),
          {
            status: 409,
            headers: { "Content-Type": "application/problem+json" }
          }
        )
      )
    );

    await expect(
      transitionFulfillmentRequest(
        "token",
        request.id,
        "Processing",
        request.version
      )
    ).rejects.toMatchObject(
      {
        status: 409,
        message: "Expected version 1 no longer matches.",
        traceId: "trace-409"
      }
    );
  });
});
