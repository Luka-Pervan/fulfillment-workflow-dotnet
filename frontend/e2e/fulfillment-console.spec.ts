import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

test("supervisor can create, replay and safely advance a synthetic request", async ({
  page,
  request
}) => {
  const externalReference = `E2E-${Date.now()}`;

  await page.goto("/");
  const initialAccessibilityResults = await new AxeBuilder({ page }).analyze();
  expect(
    initialAccessibilityResults.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? "")
    )
  ).toEqual([]);

  await page.getByRole("button", { name: "Operator" }).click();
  await expect(page.getByText(/Active as Operator/)).toBeVisible();
  const operatorAccessibilityResults = await new AxeBuilder({ page }).analyze();
  expect(
    operatorAccessibilityResults.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? "")
    )
  ).toEqual([]);

  await page.getByRole("button", { name: "Supervisor" }).click();
  await expect(page.getByText(/Active as Supervisor/)).toBeVisible();

  await page.getByLabel("External reference").fill(externalReference);
  await page.getByLabel("Postal code").fill("21000");
  await page.getByRole("button", { name: "Create request" }).click();

  await expect(page.getByText(/New request created \(HTTP 201\)/)).toBeVisible();
  await expect(
    page.getByRole("heading", { name: externalReference, exact: true })
  ).toBeVisible();

  await page
    .getByRole("button", { name: "Submit again with same key" })
    .click();
  await expect(
    page.getByText(/Existing request replayed \(HTTP 200\)/)
  ).toBeVisible();

  const requestId = await page.locator(".details-id code").textContent();
  expect(requestId).toBeTruthy();

  const externalTokenResponse = await request.post(
    "http://127.0.0.1:5080/dev/token",
    { data: { role: "Supervisor" } }
  );
  const externalToken = (await externalTokenResponse.json()) as {
    accessToken: string;
  };
  const externalTransition = await request.post(
    `http://127.0.0.1:5080/api/fulfillment-requests/${requestId}/transitions`,
    {
      data: { status: "Processing", expectedVersion: 1 },
      headers: { Authorization: `Bearer ${externalToken.accessToken}` }
    }
  );
  expect(externalTransition.status()).toBe(200);

  await page.getByRole("button", { name: "Move to Processing" }).click();
  await expect(page.getByText(/changed before this action completed/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Move to Completed" })
  ).toBeVisible();

  await page.getByRole("button", { name: "Move to Completed" }).click();
  await expect(page.getByText(/terminal state/)).toBeVisible();

  const accessibilityResults = await new AxeBuilder({ page }).analyze();
  expect(
    accessibilityResults.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? "")
    )
  ).toEqual([]);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth
      )
    )
    .toBe(true);

  await page.getByRole("button", { name: "Close" }).click();
  await page.getByRole("button", { name: "View" }).first().click();
  await expect(page.locator(".details-region")).toBeFocused();
  await expect(
    page.getByRole("heading", { name: externalReference, exact: true })
  ).toBeInViewport();
});
