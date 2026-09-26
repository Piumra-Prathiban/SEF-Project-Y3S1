# End-to-end: declining sales → promotion proposal → approval → live promotion

This document is the reproducible demonstration procedure for the complete
Marketing & Business Intelligence workflow, plus the evidence that each
architectural boundary (React → API, Flutter → API, API → PostgreSQL,
API → Agent) actually holds. It has two parts:

1. **The automated end-to-end test** — `MarketingWorkflowEndToEndTests.cs`,
   which runs this exact scenario over the real HTTP pipeline every time the
   backend test suite runs.
2. **A manual procedure** to run the same scenario by hand against a real
   running API and a real PostgreSQL database, for a live demo.

## Scenario

> A marketing/admin user wants the system to identify products with
> declining demand and propose suitable promotions.

## Where each step lives

| # | Step | Where it happens |
|---|---|---|
| 1–2 | React sends the objective; ASP.NET Core authenticates and authorizes | `POST /api/Auth/login`, then `POST /api/agents/inventory-promotion/workflows` (`[Authorize(Roles = "Staff,Administrator")]`) |
| 3–4 | An `AgentWorkflow` row is created; a structured plan is recorded | `InventoryPromotionAgentService.StartAsync` |
| 5–7 | The agent retrieves sales velocity, inventory, pricing and existing promotions | `GetSalesVelocity`, `GetInventory`, `GetProductDetails`, `GetProductPricing`, `GetActivePromotions` (Phase 09) |
| 8 | The agent drafts a proposal; deterministic validation checks it | `LocalPromotionProposalModel` + `PromotionProposalValidator` |
| 9 | The workflow pauses for a human reviewer | `AgentWorkflowStatus.AwaitingApproval` (see naming note below) |
| 10 | React displays the proposal and execution summary | `GET /api/agents/inventory-promotion/workflows/{id}` |
| 11–12 | The reviewer approves, rejects or requests revision; the API validates the decision | `POST .../approve`, `.../reject`, `.../revise` |
| 13–14 | The approved action runs through `PromotionService`; the promotion and workflow state are stored | `InventoryPromotionAgentService.ApproveAsync` |
| 15–16 | A customer client (Flutter, or React) retrieves the now-live promotion | `GET /api/promotions/{id}/products`, `GET /api/promotions/products/{productId}` (`[AllowAnonymous]`, Phase 08) |

**Naming note:** the scenario names the paused state "PendingManagerApproval".
The implemented enum value is `AgentWorkflowStatus.AwaitingApproval` (chosen
in Phase 09, before this scenario was written). They mean the same thing;
renaming the enum now would be a breaking change to the already-shipped API
contract and the React/Flutter code built against it, so this document maps
the name instead of renaming the value.

**On "Planning/Coordinator Agent":** this codebase has one agent
(`InventoryPromotionAgentService`) that both plans and executes — it writes
its structured `Plan` (see the JSON below) as its first action, before any
tool call, which is what step 4 describes. There is no separate Planning/
Coordinator *class* in the current implementation to hand off to; that
would be a bigger architectural change than this phase's scope.

## Part 1: the automated test

```
dotnet test backend/SEF_Project.Api.Tests --filter FullyQualifiedName~MarketingWorkflowEndToEndTests
```

`MarketingWorkflowEndToEndTests.DecliningSalesObjective_ShouldFlowFromReactRequest_ToApprovedCustomerFacingPromotion`
runs the whole scenario over the real ASP.NET Core pipeline (routing, JWT
auth, role authorization, model validation, `GlobalExceptionHandler`, EF
Core) using `WebApplicationFactory<Program>`, exactly as `PromotionsApiTests`
and the other API tests in this project do. It substitutes SQLite for
PostgreSQL — same EF Core, same provider-agnostic application code, faster
and isolated per test run; Part 2 below is the same scenario over a real
PostgreSQL database.

The test:

1. Seeds a Spaghetti Carbonara customer with 4 completed orders in
   September and only 3 in October (a genuine 25% decline) — this is the
   only test-only setup; everything from here on is a real HTTP call.
2. Confirms an anonymous request and a Customer-role request are both
   refused (401 / 403) *before* logging in as the reviewer.
3. Logs in for real, over HTTP, as a seeded Administrator (`POST
   /api/Auth/login`) — not a synthetic test token.
4. Starts the workflow with the exact objective from the scenario.
5. Asserts the plan, the five gather tools (all succeeded), the drafted
   proposal (Carbonara, 15% off, evidence matching the seeded data), and
   that every validation check passed.
6. Asserts the workflow is `AwaitingApproval`, then re-fetches it (as React
   would for the review screen).
7. Approves it with a comment; asserts a second approval attempt is
   refused with 409 Conflict (duplicate decisions are prevented).
8. Reads the resulting `Promotion` row directly from the same database the
   API used, confirming persistence.
9. Advances the shared test clock to the promotion's start date (the agent
   schedules new promotions to start the following day, never
   mid-instant; this is the same as a real deployment simply reaching that
   day).
10. As a **brand-new, unauthenticated** client — precisely how the Flutter
    app and the React app's customer-facing pages call these endpoints —
    fetches the product and confirms the discounted price.

Every one of the 12 "Verify" scenarios from Phase 09 (valid proposal,
invalid product, insufficient inventory, invalid discount, conflicting
promotion, malformed output, tool failure, timeout, approval, rejection,
revision, safe failure) already has its own dedicated test in
`InventoryPromotionAgentTests.cs`; this file is the single successful
end-to-end path stitched together, not a duplicate of those.

### Real captured transcript (from an actual passing run)

**Step 9–10 — workflow awaiting approval, as React's detail page renders it**
(trimmed to the parts a reviewer reads; the full response also carries every
tool call's raw arguments/result, shown in full further down):

```json
{
  "workflowId": "fce3d988-bd38-41b2-ae08-0a5115a02a76",
  "objective": "Find products with declining sales and recommend suitable promotions.",
  "status": 3,
  "plan": [
    "Retrieve sales velocity (GetSalesVelocity)",
    "Retrieve live promotions (GetActivePromotions)",
    "Retrieve inventory, product details and pricing for candidate products (GetInventory, GetProductDetails, GetProductPricing)",
    "Draft a structured promotion proposal (proposal model)",
    "Price each proposal on the server (CalculatePromotion)",
    "Validate the proposal with deterministic business rules",
    "Pause for human approval (Administrator for high-impact proposals)",
    "Re-validate and create approved promotions through PromotionService"
  ],
  "impactLevel": 0,
  "proposal": {
    "schemaVersion": "1.0",
    "summary": "1 promotion proposal(s) for declining products.",
    "proposals": [
      {
        "productId": "00000000-0000-0000-0000-000000000023",
        "productName": "Spaghetti Carbonara",
        "promotionType": "PercentageDiscount",
        "discountValue": 15,
        "startDate": "2026-10-16T00:00:00Z",
        "endDate": "2026-10-30T00:00:00Z",
        "rationale": "Units sold fell from 4 to 3 (25% lower) over the last 30 days while 35 units are available.",
        "evidence": { "unitsSold": 3, "previousUnitsSold": 4, "availableQuantity": 35 }
      }
    ]
  },
  "pricing": [
    {
      "productId": "00000000-0000-0000-0000-000000000023",
      "variants": [
        { "productVariantId": "00000000-0000-0000-0000-000000000035", "sku": "PST-CARB-R",
          "originalPrice": 1800.0, "discountAmount": 270.0, "finalPrice": 1530.0 }
      ]
    }
  ],
  "steps": [
    { "stepOrder": 1, "agentName": "Inventory & Promotion Agent", "title": "Gather sales, inventory, promotion and pricing data", "status": 2, "summary": "Retrieved sales velocity for 7 variant(s), 3 live promotion(s) and details for 5 product(s)." },
    { "stepOrder": 2, "agentName": "Inventory & Promotion Agent", "title": "Draft structured promotion proposal", "status": 2, "summary": "1 promotion proposal(s) for declining products." },
    { "stepOrder": 3, "agentName": "Inventory & Promotion Agent", "title": "Price proposals on the server", "status": 2, "summary": "Priced 1 of 1 proposal(s)." },
    { "stepOrder": 4, "agentName": "Deterministic Validator", "title": "Validate proposal against business rules", "status": 2, "summary": "All 9 checks passed." },
    { "stepOrder": 5, "agentName": "Human Reviewer", "title": "Human approval required (low impact: Staff or Administrator)", "status": 0, "summary": "1 promotion(s) awaiting review." }
  ]
}
```

**Step 13–14 — completed workflow with the recorded approval decision**
(validation results and the approval record; `status: 4` is `Completed`):

```json
{
  "validationResults": [
    { "stepOrder": 6, "validatorName": "ProductExists", "isValid": true, "message": "Spaghetti Carbonara: active product found." },
    { "stepOrder": 6, "validatorName": "NoConflict", "isValid": true, "message": "Spaghetti Carbonara: no live promotion." },
    { "stepOrder": 6, "validatorName": "PromotionDates", "isValid": true, "message": "Spaghetti Carbonara: runs for 14 days." },
    { "stepOrder": 6, "validatorName": "ServerPricing", "isValid": true, "message": "Spaghetti Carbonara: server priced 1 variant(s); every final price stays above zero." },
    { "stepOrder": 6, "validatorName": "ProposalLimit", "isValid": true, "message": "1 proposal(s) within the limit." },
    { "stepOrder": 6, "validatorName": "SufficientInventory", "isValid": true, "message": "Spaghetti Carbonara: 35 available, above the reorder level of 10." },
    { "stepOrder": 6, "validatorName": "DiscountLimits", "isValid": true, "message": "Spaghetti Carbonara: 15% discount is within the 30% limit." },
    { "stepOrder": 6, "validatorName": "ReviewerConstraints", "isValid": true, "message": "Spaghetti Carbonara: not excluded by the reviewer." }
  ],
  "approvals": [
    { "status": 1, "requestedAt": "2026-10-15T12:00:00", "reviewedByUserId": 1,
      "reviewedAt": "2026-10-15T12:00:00", "comment": "Approved for the autumn promotion push." }
  ],
  "errors": [],
  "createdPromotionIds": ["773dc8a4-8a0f-435e-9b48-c2f2a1059d23"],
  "finalOutcome": "Created 1 promotion(s) after approval by user 1: Spaghetti Carbonara 15% off."
}
```

**Step 15–16 — what an unauthenticated customer client (Flutter/React) now
sees**, fetched via `GET /api/promotions/products/{productId}` with **no
Authorization header at all**:

```json
{
  "productId": "00000000-0000-0000-0000-000000000023",
  "productName": "Spaghetti Carbonara",
  "hasActivePromotion": true,
  "promotions": [
    { "id": "773dc8a4-8a0f-435e-9b48-c2f2a1059d23", "name": "Spaghetti Carbonara 15% off",
      "type": 0, "discountValue": 15.0,
      "startDate": "2026-10-16T00:00:00", "endDate": "2026-10-30T00:00:00" }
  ],
  "variants": [
    { "productVariantId": "00000000-0000-0000-0000-000000000035", "sku": "PST-CARB-R",
      "originalPrice": 1800.0, "discountAmount": 270.00, "finalPrice": 1530.00,
      "promotionId": "773dc8a4-8a0f-435e-9b48-c2f2a1059d23",
      "promotionName": "Spaghetti Carbonara 15% off", "currency": "LKR" }
  ]
}
```

To see the complete transcript, including every tool call's full arguments
and results, re-run the command above with `--logger "console;verbosity=detailed"`.

## Part 2: manual procedure against a real, running API + PostgreSQL

1. **Start PostgreSQL** and apply migrations (see the repo `README.md`):
   ```
   dotnet ef database update --project backend/SEF_Project.Api
   ```
2. **Give one seeded user the Administrator role** (registration always
   creates a Customer). Register a user through the API, then in the
   database:
   ```sql
   UPDATE "Users" SET "RoleId" = 3 WHERE "Email" = 'you@example.com';
   ```
3. **Run the API:** `dotnet run --project backend/SEF_Project.Api` (listens
   on `http://localhost:5193`).
4. **Log in** (React and Flutter both do this the same way):
   ```
   curl -s -X POST http://localhost:5193/api/Auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"you@example.com","password":"..."}'
   ```
   Copy the `token` from the response.
5. **Start the workflow:**
   ```
   curl -s -X POST http://localhost:5193/api/agents/inventory-promotion/workflows \
     -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
     -d '{"objective":"Find products with declining sales and recommend suitable promotions."}'
   ```
   Note the `workflowId`. If nothing qualifies yet, place a few real orders
   for the same product in two different weeks first (via the Orders API or
   the seeded checkout flow) so there is a genuine decline to find.
6. **Watch it in the React app:** open `/marketing/agent/<workflowId>`
   (Phase 10) — the same data the curl call above returned, rendered as a
   page.
7. **Approve, reject, or request a revision** from that page, or directly:
   ```
   curl -s -X POST http://localhost:5193/api/agents/inventory-promotion/workflows/<workflowId>/approve \
     -H "Authorization: Bearer <token>" -H "Content-Type: application/json" -d '{"comment":"..."}'
   ```
8. **Confirm PostgreSQL now holds it:**
   ```sql
   SELECT "Id", "Name", "DiscountValue", "IsActive", "StartDate", "EndDate" FROM "Promotions" ORDER BY "CreatedAt" DESC LIMIT 1;
   SELECT "Status", "FinalOutcome" FROM "AgentWorkflows" WHERE "Id" = '<workflowId>';
   ```
9. **See it as a customer**, with no login at all:
   ```
   curl -s http://localhost:5193/api/promotions/products/<productId>
   ```
   Once the promotion's start date arrives, this response is exactly what
   the Flutter app's `ProductDetailScreen` (Phase 08) and its
   `PromotionIndicator`/`PriceTag` widgets render.

## Verifying the four boundaries

| Boundary | How it is verified |
|---|---|
| **React → ASP.NET Core** | React's `apiRequest` (`src/services/api.js`) is the only function in the whole frontend that calls `fetch`; `ClientBoundaryTests.ReactSource_ShouldSendEveryRequestThroughTheSingleApiClient` fails the build if any other file ever calls `fetch` directly. |
| **Flutter → ASP.NET Core** | Flutter's `ApiClient` (`lib/core/api/api_client.dart`) is the only file allowed to touch the `http` package; `ClientBoundaryTests.FlutterSource_ShouldSendEveryRequestThroughTheSingleApiClient` enforces that the same way. |
| **ASP.NET Core → PostgreSQL** | Only `SEF_Project.Api` references `Npgsql`/`AppDbContext`; `ClientBoundaryTests.ReactSource_ShouldContainNoDirectDatabaseOrAgentAccess` and its Flutter counterpart scan both client codebases and fail if either ever contains a connection string, a raw SQL statement, or a reference to `AppDbContext`. |
| **ASP.NET Core → Agentic AI** | The `SEF_Project.Api.AI.InventoryPromotion` namespace is never referenced outside the API project; the same two `ClientBoundaryTests` also fail if either client codebase ever names `InventoryPromotionAgentService` or `PromotionAgentToolRegistry` directly, and the agent itself only ever writes through `IPromotionService` (never `AppDbContext`) once a proposal is approved. |

Run just these checks with:

```
dotnet test backend/SEF_Project.Api.Tests --filter FullyQualifiedName~ClientBoundaryTests
```

## What is and is not covered here

- **Flutter was not run when this phase was written** (no Flutter SDK on the machine). It was
  run later, in Phase 13: 38 / 38 Flutter tests pass. Step 15–16 is verified by
  exercising the exact HTTP contract Flutter's `ApiClient` and
  `ProductDetailScreen` depend on, with real data produced by the real
  agent and a real approval, which is the strongest check available
  without a device or emulator.
- **React was not driven through a browser** for this phase either; its
  contribution is verified the same way (the same JSON its `agentService.js`
  and `AgentWorkflowDetailPage.jsx` consume, produced end to end).
- **All workflow states and every approval decision are recorded** in the
  existing `AgentWorkflow` / `AgentWorkflowStep` / `AgentToolExecution` /
  `AgentValidationResult` / `AgentApproval` / `AgentWorkflowError` tables
  (Phase 09); nothing new was added to persist state, because that
  contract already carries everything this scenario needs, as the
  transcript above shows.
