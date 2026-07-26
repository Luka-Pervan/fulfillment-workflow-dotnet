# Fulfillment Operations Console

The React and TypeScript console is a deliberately thin portfolio interface
over the real ASP.NET Core workflow API. Its purpose is to make backend
reliability behavior visible and testable without introducing a second domain
model or a frontend framework stack that the sample does not need.

## Scope

The console supports:

- Development-only `Operator` and `Supervisor` identities from `/dev/token`
- In-memory bearer tokens; no browser storage or cookies
- Filtered, paged request lists and explicit refresh
- Idempotent request creation with a stable key across retries
- Visible distinction between HTTP `201 Created` and HTTP `200` replay
- Request details and Supervisor-only workflow transitions
- Optimistic concurrency through the current `expectedVersion`
- Explicit HTTP `401`, `403`, `409`, `422`, and `429` feedback
- Responsive semantic HTML, keyboard focus, reduced-motion support, and an
  automated axe scan for serious or critical issues

It intentionally has no router, global state library, design system, analytics,
or AI feature. Those additions would increase the review surface without
providing stronger evidence for this bounded workflow.

## Local development

Start the API from the repository root:

```bash
sh scripts/run-development-api.sh
```

Then start Vite:

```bash
cd frontend
npm ci
npm run dev
```

Open `http://127.0.0.1:5173`. Vite proxies `/api`, `/dev`, and `/health` to the
API at `http://127.0.0.1:5080`.

## Production-shaped static serving

`npm run build` writes hashed static assets to `frontend/dist`. The API project
includes that directory when publishing locally, and the multi-stage Docker
build copies the same output into the runtime image at `wwwroot`. ASP.NET Core
then serves the UI and API from one origin with a restrictive Content Security
Policy and basic response hardening headers.

The included role picker is not a production sign-in flow. `/dev/token` is
absent outside the Development environment. A real deployment must connect the
API and UI to the configured external identity provider and should use an
appropriate browser session or token architecture after a dedicated threat
model review.

## Verification

```bash
cd frontend
npm run quality
npm audit --audit-level=high
npx playwright install chromium
npm run e2e
```

Unit tests cover API response semantics, stable idempotency keys, filtering,
paging, permissions, and error language. Playwright starts a real API with a
temporary SQLite database and verifies create, replay, state transitions, and
accessibility in Chromium.

Only synthetic test data is used. The console contains no employer assets,
customer information, proprietary UI, or production credentials.

## Feature Management Candidacy

**No.** This is a standalone portfolio sample with a single documented UI
surface and no staged rollout population. A feature flag would add operational
machinery without a real deployment or transition requirement. If the console
became a deployed multi-tenant product, rollout and kill-switch needs should be
re-evaluated against concrete users, dependencies, and fallback behavior.
