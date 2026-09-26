# Member 4: Viva Study List (Marketing & Business Intelligence)

Paths are under `backend/SEF_Project.Api/` unless stated. Open the file, read it, and be able to explain it
without notes. Full detail is in [MEMBER4_TECHNICAL_DOCUMENTATION.md](MEMBER4_TECHNICAL_DOCUMENTATION.md).

## Be ready to say plainly

* The agent's proposal model is a **deterministic local policy**, not an LLM. The design lets an LLM replace it
  without touching tools, validation or approval.
* **Coupons** have tables, constraints and seed data only; no endpoints or redemption logic.
* **Checkout does not apply promotions yet.** Promotions are exposed through pricing endpoints.
* Phases 12–13 are on the feature branch only, unreviewed, and `main` has moved on (renamed members, new seed migration).
* There is no peer review or PR of your own in GitHub; do not claim otherwise.

## 1. Controllers: `Controllers/`

* Thin: read the user id from `ClaimTypes.NameIdentifier`, pass a flag, return DTOs; a service returning `null` → 404.
* `[ApiController]` gives automatic 400 model-validation. `[Authorize]` at class level, `[AllowAnonymous]` only on the four public promotion reads.
* Files: `PromotionsController`, `CampaignsController`, `AnalyticsController`, `InventoryPromotionAgentController`.
* Q: *Why is `GET /api/promotions` public but `POST` not?* Customers must see live promotions; only staff manage them. Non-staff get the "live" filter applied in the service, not the controller.

## 2. DTOs: `DTOs/`

* Classes with DataAnnotations (`[Required]`, `[StringLength]`, `[Range]`, `[MaxLength]`, `[EnumDataType]`) plus `IValidatableObject` for cross-field rules (`PromotionRequest.Validate`).
* Entities are never returned. `PagingLimits` caps page at 10,000 and size at 100.
* Q: *Why three layers of validation (DTO, service, DB)?* DTO gives fast, friendly errors; services enforce rules needing data (campaign exists, dates nest); DB CHECKs protect against every other writer.
* Know the enum-as-number wire format and that `focus` is `0/1`.

## 3. Services: `Services/Marketing`, `Services/Analytics`

* Controller → Service → `AppDbContext`, registered `Scoped` in `Program.cs`.
* `PromotionService`: visibility, sync of join rows by difference, transactional batch create. `CampaignService`: terminal statuses, date coverage. `PromotionDiscountCalculator`: pure, unit-tested pricing. `PromotionOfferService`: best promotion per variant.
* Q: *Walk through pricing a variant.* Section 3 of the technical doc (validate → percent or fixed → round half away from zero → cap at price → best promotion by largest discount).
* `AnalyticsService`: database-side aggregation; know the **two-window** demand method and why Phase 13 changed the queries.

## 4. EF relationships: `Data/Configurations/MarketingConfigurations.cs`

* Campaign 1—* Promotion (`Restrict`); Promotion *—* Product and Category through composite-key join tables (cascade from promotion, restrict from product/category); Promotion 1—* Coupon; Coupon 1—* CouponRedemption.
* Rule of the project: owned children cascade, cross-component and reference FKs are `Restrict`.
* Enums stored as strings; money `HasPrecision(18, 2)`; CHECK constraints use quoted identifiers.
* Q: *Why `Restrict` on Promotion → Campaign?* Prevents silently deleting promotions (history); the service also refuses deleting a campaign that has promotions.

## 5. Indexes

* `Promotions(IsActive, StartDate, EndDate)` for "live" queries; `Campaigns(Status)` and `(StartDate, EndDate)`; unique `Coupons(Code)`; unique `CouponRedemptions(CouponId, OrderId)` (one redemption per order, also serves usage-limit lookups); `CouponRedemptions(CustomerId)` and `(RedeemedAt)`.
* Q: *Why no index on `Orders.PlacedAt`?* Measured on 200k orders: no benefit, so not added.

## 6. Migrations: `Migrations/`

* `20260923072703_AddMarketingDomainConstraints`: replaces `IsActive` with `Status` (`true → Active`, `false → Paused`, reversible), adds 8 CHECK constraints and indexes.
* Rules: change the model first, then `dotnet ef migrations add`; commit migration + Designer + snapshot together; never edit an applied migration.
* Q: *Why does the discount CHECK cast to `REAL`?* SQLite (used in tests) stores decimals as text.
* Know that `main` added `FashionMarketingSeed`.

## 7. React state: `frontend/SEF-Project/src/`

* No state library. Local state + `useAsync` (loads, reloads, ignores stale responses) + `useListQuery` (filters, sort and page live in the URL).
* `services/api.js` is the only `fetch`; features call small service modules. `AuthContext` keeps the token in memory only.
* `ProtectedRoute roles={MANAGER_ROLES}` guards navigation; **the API is the real enforcement**.
* Q: *What happens on a failed request?* `apiRequest` throws an `Error` with `status`/`data`; pages show `detail`/`title` with a retry.

## 8. Flutter API integration: `mobile/.../lib/`

* Screens → repository → `ApiClient` (only file importing `http`). Token attached only over HTTPS or a local host; cleared on 401; 15 s timeout; failures become `ApiException`.
* State: `ChangeNotifier` + sealed `LoadState` (Loading / Loaded / Failed) with `ListenableBuilder`.
* The device never calculates prices; it shows the server's `finalPrice`.
* Q: *Why an interface for the repository?* Tests substitute a fake; the UI does not know about HTTP.

## 9. JWT authorization

* Program.cs validates issuer, audience, lifetime and signing key. Roles come from the `role` claim (Customer 1, Staff 2, Administrator 3); registration always creates a Customer.
* Verified by the authorization matrix (27 endpoints × 4 identities) and JWT rejection tests (expired, wrong key, issuer, audience, unsigned, tampered).
* Q: *401 vs 403?* 401 = not authenticated or bad token; 403 = authenticated but wrong role.
* Known gap: `AuthService` hard-codes a 60-minute expiry.

## 10. Agent tools: `AI/InventoryPromotion/PromotionAgentTools.cs`

* Six read-only tools: `GetSalesVelocity`, `GetInventory`, `GetActivePromotions`, `GetProductDetails`, `GetProductPricing`, `CalculatePromotion`.
* `PromotionAgentToolRegistry` runs every call: allow-list, strict argument validation (no retry), output check, timeout (5 s), ≤ 2 retries for transient errors, redaction, audit row per attempt.
* Q: *How do you know the model cannot reach the database?* It only receives a `PromotionAgentContext`; a reflection test fails if any tool or model takes a `DbContext`; only the orchestrator does.

## 11. Agent state

* Statuses: Planning → InProgress → AwaitingApproval → Completed / Cancelled / Failed. Steps, tool executions, validation results, approvals and errors are stored in the shared `Agent*` tables.
* No chain-of-thought is stored; malformed model output is discarded; stored JSON is redacted.
* Failures become a persisted `AgentWorkflowError` with a type (`ToolTimeout`, `MalformedOutput`, `ValidationFailed`, `StaleProposal`, …) and the workflow ends `Failed` with nothing changed.

## 12. Deterministic validation: `PromotionProposalValidator.cs`

* Strict schema parse (`AgentJson.StrictOptions`: unknown properties rejected), then 9 rules: proposal limit, product exists, reviewer exclusions, sufficient inventory, discount limits, dates, no conflict, **evidence matches data**, server pricing.
* Fail closed: nothing is repaired. Facts are rebuilt from tool data, never from the model.
* Q: *Why check "evidence matches data"?* It stops the model inventing figures to justify a proposal.
* Approval re-runs validation with fresh data (except the evidence rule), so a stale proposal fails as `StaleProposal`.

## 13. Human approval

* Low impact (all discounts < 20% and ≤ 3 proposals): Staff or Administrator. High impact: Administrator only (409 otherwise). ≤ 3 revisions.
* Approve → re-validate → `PromotionService.CreatePromotionsAsync` (atomic). The agent never writes directly.
* **Concurrency fix (Phase 13):** decisions are claimed with a conditional `UPDATE … WHERE Status = 'Pending'` inside a transaction, so two simultaneous reviews cannot both act.
* Q: *What was the bug and how did you prove it?* Three tests failed first (double-created promotions, reject overriding approve, revise restarting a completed run); the atomic claim made them pass.

## 14. Testing

* Backend 439, React 131, Flutter 38 passing (2026-09-26). Service tests on SQLite in-memory; API tests through `WebApplicationFactory<Program>` with real pipeline, fixed clock and real fixture users per role.
* Bugs the tests found (know at least three): test-factory FK bug, approval race, unbounded page number, Flutter semantics-label expectation.
* Know a "control case" and a **mutation check**: removing the fix made 10 paging tests fail.
* Provider differences: SQLite vs PostgreSQL (decimal sums and sorts, CHECK casts), so key behaviour was also verified on real PostgreSQL.
* Architecture tests: `ClientBoundaryTests`; end-to-end: `MarketingWorkflowEndToEndTests`.

## 15. CI

* `.github/workflows/backend-ci.yml`: on every push and on PRs to `main`/`develop`: restore, build (Release), test the .NET solution. 13 / 13 runs green on your branch.
* Honest limits: it does not run React or Flutter tests; the PR filter names `develop` while the branch is `development` (pushes still trigger it).
* Q: *What would you add?* A frontend job (`npm test`, `npm run lint`, `npm run build`) and a Flutter job.

## 16. Git workflow

* Feature branch `feature-Marekting-and-buisness-intelligence` → lead merges into `development` → PR to `main` (PR #64). Others' issues used `Closes #N`; yours did not link issues.
* Your history: 12 commits, Phase 1 and Phases 3–13 (no Phase 2 commit); subjects are "Phase N" with a descriptive body.
* Be ready to explain: what a merge conflict is and how `aa69629` resolved one; why you merge `main` into your branch before a PR; `git log origin/main..HEAD` to see unmerged work.
* Q: *What would you improve?* Descriptive commit subjects, issue links, your own PRs with peer review.

## Suggested 3-minute demo

1. Log in as Administrator; open Campaigns → Promotions; show a promotion's live state.
2. Analytics: show demand with the trend column.
3. Start an agent workflow; open the detail page: plan, tool calls, validation results, evidence.
4. Approve it; show the created promotion.
5. As an anonymous client call `GET /api/promotions/products/{id}` (or the Flutter product screen) and show the discounted price.
6. Show a Staff account being refused a high-impact approval (409) and a Customer getting 403 on analytics.
