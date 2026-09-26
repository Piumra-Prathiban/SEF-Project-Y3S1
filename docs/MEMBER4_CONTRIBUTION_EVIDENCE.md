# Member 4: Individual Contribution Evidence (Marketing & Business Intelligence)

**How this was built.** Every fact below comes from `git log` / `git show` on the local repository, the public
GitHub REST API (pull requests, issues, comments, Actions runs; read on 2026-09-26) or the code itself.
Nothing is estimated or invented. Where evidence **does not exist**, this document says so (section 8) instead
of filling the gap. Repository: `Piumra-Prathiban/SEF-Project-Y3S1`. Author identity in Git: `nimmi-codes`.

Companion files: [Technical documentation](MEMBER4_TECHNICAL_DOCUMENTATION.md),
[AI usage log guide](MEMBER4_AI_USAGE_LOG_GUIDE.md), [Viva study list](MEMBER4_VIVA_STUDY_LIST.md).

## 1. Summary

| Item | Fact | Source |
|---|---|---|
| Commits authored | **12** (Phase 1, Phases 3–13), 2026-09-23 → 2026-09-26 | `git log --author` |
| Not counted | 2 GitHub Desktop **stash** entries (`bd67917`, `ca3fef5`), which are not contributions | `git log --all` |
| There is **no Phase 2 commit** | Phase 2 was never committed under this identity | `git log` |
| Branch | `feature-Marekting-and-buisness-intelligence` (local and `origin`) | `git branch -a` |
| In `origin/main` | Phases 1–11, via PR #64 (merged 2026-09-25) | `git branch --contains` |
| **Not** in `origin/main` | Phases 12 and 13 (`fa8fea5`, `84accb7`): on the feature branch only | `git log origin/main..HEAD` |
| CI | GitHub Actions passed on **all 13 runs** on this branch (Phases 1, 3–13 + the base merge) | Actions API |
| Issues assigned to you | #37, #45, #46, #47, #48 (all closed) and #5 (research, closed) | Issues API |
| Pull requests you opened | **0** | Pulls API |
| Reviews / review comments on your work | **0** | Pulls/Issues comments API |

## 2. Commit history (real)

Commit subjects are only "Phase N"; the descriptive sentence is in each commit body (shown below).
Times are +05:30. Lines are insertions/deletions from `git show --shortstat` and include generated files
(for example the 2,171-line migration `.Designer.cs` in Phase 1).

| # | Commit | Date | Body (what the commit says) | Files | +/− | CI |
|---|---|---|---|---|---|---|
| 1 | `24fd4f6` | 09-23 13:07 | Phase 1: Creating marketing db foundation | 8 | +2925 / −15 | pass |
| 2 | `25904f4` | 09-24 18:47 | Phase 3: Implementing business logic of promotion | 9 | +846 | pass |
| 3 | `1972931` | 09-24 19:28 | Phase 4: Creating APIs for marketing | 23 | +1941 / −46 | pass |
| 4 | `66a2125` | 09-24 22:09 | Phase 5: Implementing the analytics for marketing | 8 | +1904 | pass |
| 5 | `9c222c3` | 09-24 22:45 | Phase 6: Creating react UI for marketing | 49 | +4946 / −196 | pass |
| 6 | `7c6fe34` | 09-24 22:59 | Phase 7: Creating react dashboard for marketing | 27 | +2677 / −7 | pass |
| 7 | `c1d54bd` | 09-24 23:20 | Phase 8: Creating flutter UI for promotions | 30 | +2534 / −129 | pass |
| 8 | `7f937a7` | 09-24 23:44 | Phase 9: Creating promotion agent (to suggest promotions) | 14 | +3675 / −9 | pass |
| 9 | `9589357` | 09-25 00:10 | Phase 10: Creating react UI to approve/reject/revise of the agent's suggestion | 12 | +1968 | pass |
| 10 | `6a6e039` | 09-25 00:26 | Phase 11: Connecting all platforms (React, Flutter, backend, db, agent) into one workflow | 4 | +775 / −4 | pass |
| 11 | `fa8fea5` | 09-26 10:35 | Phase 12: Testing the marketing component | 5 | +607 / −10 | pass |
| 12 | `84accb7` | 09-26 12:06 | Phase 13: Checking the security and performance of the marketing component | 13 | +668 / −61 | pass |

Aggregate over these 12 commits (`git show --numstat`, grouped by folder; generated migration files included
under "migrations"):

| Area | File touches | Insertions | Deletions |
|---|---|---|---|
| Backend API (controllers, DTOs, services, models) | 59 | 3,660 | 72 |
| Backend agent (`AI/InventoryPromotion`) | 6 | 2,223 | 15 |
| Backend tests | 22 | 4,657 | 49 |
| Migrations (mostly generated) | 3 | 2,543 | 9 |
| React (source) | 66 | 7,471 | 203 |
| React tests | 19 | 2,315 | 0 |
| Flutter (source) | 17 | 1,394 | 106 |
| Flutter tests | 8 | 726 | 23 |
| Documentation | 2 | 477 | 0 |

Line counts show effort and scope, not quality, and should not be quoted as a measure of either.

## 3. Evidence by area

### Backend

* **Promotion pricing (Phase 3, `25904f4`):** `PromotionDiscountCalculator`, `PromotionPricingService`,
  `POST /api/promotions/{id}/calculate-discount`, DTOs; 27 test methods.
* **Marketing APIs (Phase 4, `1972931`):** `PromotionsController`, `CampaignsController`, `PromotionService`,
  `CampaignService`, request/response/query DTOs, `PagedResponse`, `MarketingDates`; API tests through a real
  `WebApplicationFactory` host.
* **Analytics (Phase 5, `66a2125`):** `AnalyticsController`, `AnalyticsService` (database-side aggregation,
  paging, sorting), query/response DTOs.
* **Customer offers (Phase 8, `c1d54bd`):** `PromotionOfferService` and the public offer endpoints Flutter uses.
* **Hardening (Phase 13, `84accb7`):** paging cap (`PagingLimits`), bounded ID lists, concurrency-safe approval
  claim, analytics query rewrite.

### Database

* **Phase 1 (`24fd4f6`):** migration `20260923072703_AddMarketingDomainConstraints` (replaces
  `Campaigns.IsActive` with `Status`, with a reversible data backfill; adds 8 CHECK constraints and the indexes
  in `MarketingConfigurations.cs`), seed data additions, `Enums.cs`, and `MarketingDatabaseModelTests` (11 tests).
* **Verified against a real PostgreSQL database** (Phase 12): migration applied, foreign keys and
  `ON DELETE RESTRICT`, the CHECK constraint rejecting a 150% discount, indexes, seed counts, and the
  backfill on real pre-existing rows. (This was run in the working session; the outcome is recorded in the
  Phase 12 report, not in a Git artefact.)
* **Measured, not assumed (Phase 13):** on a scratch PostgreSQL database with 200k orders, an index on
  `Orders.PlacedAt` gave no benefit and was **not** added. The scratch database was dropped afterwards.

### React

* **Phase 6 (`9c222c3`):** shared UI kit (`AppLayout`, `Pagination`, `FormField`, `StatusViews`, …), hooks
  `useAsync` / `useListQuery`, and the promotion and campaign list/detail/form pages with tests.
* **Phase 7 (`7c6fe34`):** dashboard, analytics and reports pages, five analytics sections, in-house chart
  components (`components/charts`), KPI cards, date-range hook.
* **Phase 10 (`9589357`):** agent workflow list, start and detail pages with approve / reject / revise.
* **Phase 12 (`fa8fea5`):** `CampaignListPage.test.jsx`, `CampaignDetailPage.test.jsx` (12 tests).
* Phase 6 also touched shared files (`App.jsx`, `AppLayout`, `LoginPage`, `HomePage`, `main.jsx`), so it is a
  cross-component change. On `main` the frontend was later rebuilt by another member (`56f5813`,
  "Remake the whole frontend"); 42 marketing files are still present on `main`.

### Flutter

* **Phase 8 (`c1d54bd`):** `ApiClient`, `ApiException`, `TokenStore`, `AppConfig`, formatters, `AsyncController`,
  repository + models, three screens, widgets, and 6 test files.
* **Phase 13 (`84accb7`):** the first time the Flutter tests were actually run (SDK installed in a temporary
  folder): 36 of 37 passed; one wrong test expectation was fixed. Final: 38 / 38.

### Agentic AI

* **Phase 9 (`7f937a7`):** orchestrator, tool registry and 6 tools, strict proposal parsing, deterministic
  validator, replaceable model boundary, controller, DTOs, agent tests (25 + API tests), and
  `backend/INVENTORY_PROMOTION_AGENT.md`.
* **Phase 10 (`9589357`):** the human-approval UI.
* **Phase 11 (`6a6e039`):** `MarketingWorkflowEndToEndTests`, `ClientBoundaryTests`, and
  `backend/MARKETING_E2E_WORKFLOW.md`.
* **Phase 12 / 13:** approval-authorization test, atomic batch-create test, the approval race fix with 3
  regression tests, and architecture guards (no `DbContext` in tools/model; registered tools = allow-list).
* **Design fact worth stating accurately:** the registered proposal model is a **deterministic local policy**
  behind `IPromotionProposalModel`. No LLM is called at runtime.

### Testing

Full run on 2026-09-26: backend **439 / 439**, React **131 / 131**, Flutter **38 / 38**. Test files you added or
extended are listed in section 19 of the technical documentation. Test-writing commits: Phases 3, 4, 5, 6, 8, 9,
11, 12, 13.

## 4. Issues

Your assigned issues (from the GitHub Issues API). **No commit message references an issue number**, so the
mapping below is by title versus commit body; it is not a recorded link.

| Issue | Title | State | Closest commit(s) |
|---|---|---|---|
| #37 | Marketing Database Implementation | closed | Phase 1 `24fd4f6` |
| #45 | Implement Promotion Domain and Business Rules | closed | Phase 3 `25904f4` |
| #48 | Promotions API endpoint testing | closed | Phase 4 `1972931` (API tests), later extended in Phases 6 and 8 |
| #46 | React Marketing Management | closed | Phases 6–7 (`9c222c3`, `7c6fe34`) |
| #47 | Customer Promotions through Flutter | closed | Phase 8 `c1d54bd` |
| #5 | Website research (course task) | closed | none |

**Work with no matching issue:** analytics (Phase 5), the Inventory & Promotion Agent and its UI (Phases 9–11),
testing (Phase 12) and the security/performance review (Phase 13). The parent epic **#11 "Marketing & Business
Intelligence" is still open and unassigned**. None of the issues has any comment.

## 5. Pull requests

* You opened **none**. Your commits reached `main` through the lead's **PR #64** ("Development", `development →
  main`, merged 2026-09-25), which contains merge `acb4f79` (your branch → `development`) and conflict-resolution
  merge `aa69629`.
* `aa69629` ("merged the marketing and buisness inteligence and resolved conflicts") edited files of yours,
  including `AnalyticsServiceTests` (164 lines), `PromotionsApiTests`, `PromotionPricingServiceTests`,
  `InventoryPromotionAgentTests`, `PromotionOffersApiTests` and `AnalyticsService`. This is evidence of real
  integration work by someone else on your code.
* **Phases 12 and 13 have no pull request** and are not on `main`.

## 6. Code review evidence

**There is no peer code-review evidence in GitHub for your work:** PR #64 has 0 reviews and 0 comments, and no
issue has comments. Presenting anything else would be false. What does exist:

| Evidence | What it shows | What it is not |
|---|---|---|
| CI green on 13 / 13 runs | the build and 439-test backend suite passed on every push | not a human review |
| Integration merge `aa69629` by another member | your code was reconciled with other components | not a review of your design |
| Phase 13 security and performance review | a structured self-review with reproduced bugs, measurements and regression tests | a **self**-review |

**To create genuine evidence:** open a pull request for Phases 12–13 into `development` and ask a teammate to
review it; record their comments and your replies. Do this *after* merging `main` (see section 9).

## 7. Challenges and solutions (each backed by a commit or file)

| Challenge | What happened | Solution | Evidence |
|---|---|---|---|
| Tests on SQLite, production on PostgreSQL | SQLite cannot `SUM` or `ORDER BY` decimals and stores them as text, so CHECK constraints and aggregates behave differently | Constraint casts to `REAL`; money aggregated as `double` and rounded; sorts cast; then re-verified on real PostgreSQL | `MarketingConfigurations.cs` comments, `AnalyticsService` remarks |
| Test helper created tokens for users that did not exist | The first approval test hit a foreign-key failure (`ReviewedByUserId`) that no earlier test could reach | Test factory now seeds one real user per role and mints tokens for those ids; also stopped hiding real errors in test logging | `fa8fea5`, `MarketingApiFactory.cs` |
| A security test that passed for the wrong reason | The oversize-list test returned 400 because the enum was sent as text, not because of the list | Fixed the payload and added a control case at the limit | `MarketingSecurityTests` |
| Duplicate promotions on concurrent approval | Two reviewers could both pass the "is pending" check | Reproduced with 3 failing tests first; then an atomic conditional `UPDATE` inside a transaction | `84accb7`, `InventoryPromotionAgentService.DecideAtomicallyAsync` |
| A page number could crash a public endpoint | `page=2147483647` overflowed `Skip`; PostgreSQL returned "OFFSET must not be negative" | Cap of 10,000 in DTOs and services; mutation-tested | `84accb7`, `PagingLimits.cs` |
| Slow analytics at scale | Correlated subqueries: demand-by-trend took 6–18 s (the agent's tool timeout is 5 s) | Aggregate once with `GROUP BY` + left join; first alternative measured slower and was discarded; index measured and rejected | `84accb7`, benchmark table in the technical doc |
| Flutter tests were never run | No SDK was available for several phases | Installed the SDK temporarily in Phase 13; found and fixed one wrong assertion | `84accb7` |
| Naming mismatch with the scenario | The brief says "PendingManagerApproval"; the shipped enum is `AwaitingApproval` | Kept the enum (renaming would break the shipped API) and documented the mapping | `MARKETING_E2E_WORKFLOW.md` |
| Integration with other components | Other members renamed `TotalCount` → `TotalItems` and `Inventory` → `InventoryStock`, which your Phase 12–13 code still uses | Merge `main` into the branch and fix the two names before opening a PR | section 9 |

## 8. Gaps in the evidence (do not paper over these)

* No peer review, no PR of your own, no issue comments.
* No commit links an issue; several pieces of work have no issue.
* Commit subjects are just "Phase N".
* No Phase 2 commit.
* Phases 12–13 are unmerged and unreviewed.
* CI covers only the .NET suite; React and Flutter results come from local runs.
* The PostgreSQL verification and benchmarks were run in a working session; their numbers are recorded in the
  technical documentation but are not reproducible from Git alone (the scratch database and harness were
  deleted).
* **Learning** is deliberately not written here. See section 10.

Ways to improve the record honestly, going forward: reference issues in commit messages (`Closes #N`), open a PR
for the remaining work and request a review, and open issues for untracked work **dated when you create them**,
not backdated.

## 9. Integration status and pre-PR checklist

Verified in Git on 2026-09-26: `origin/main` is at `56f5813`; `git log origin/main..HEAD` shows exactly your two
unmerged commits; `main` renamed `PagedResponse.TotalCount → TotalItems` and `ProductVariant.Inventory →
InventoryStock` (your Phase 13 `AnalyticsService` and several tests use the old names). The integration merge also added the
migration `20260925175045_FashionMarketingSeed`, which changes marketing seed data that some of your tests may
rely on. Suggested order:

1. Commit or stash any local work; `git fetch origin`.
2. `git merge origin/main` on your branch; resolve conflicts (expect `AnalyticsService`, `PromotionService`,
   `CampaignService`, `InventoryPromotionAgentTests`, `MarketingApiFactory`).
3. Run `dotnet test SEF-Project.sln`, `npm test`, `flutter test` again.
4. Push and open a pull request into `development`; request a teammate review.

(These are suggestions; nothing was run for you.)

## 10. Learning: to write in your own words

Learning and reflection must be yours. Prompts, each answerable from the evidence above:

* What did the approval race teach you about "check then act" logic, and how would you explain the fix?
* Why can a test pass while proving nothing (the enum-as-text case), and how do control cases help?
* What did measuring (the index, the failed rewrite) change about how you decide on a performance change?
* What is the difference between validating in the DTO, the service, and the database, and why keep all three?
* Why must an agent's tools be allow-listed and read-only, and what does "fail closed" mean in practice?
* What would you do differently about issues, commit messages and reviews in the next project?
