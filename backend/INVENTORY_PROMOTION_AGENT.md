# Inventory & Promotion Agent

## Responsibility

The Inventory & Promotion Agent (Marketing & Business Intelligence) analyses sales
velocity, inventory, live promotions, product details and pricing, and produces
**structured promotion proposals**. It only proposes. A person approves the
proposal, and only then are promotions created, and only through
`PromotionService`. The agent never gets the `AppDbContext`, cannot run SQL,
cannot call arbitrary APIs, and cannot change promotions or prices.

Code: `SEF_Project.Api/AI/InventoryPromotion/`, API:
`Controllers/InventoryPromotionAgentController.cs`, contracts:
`DTOs/Agents/InventoryPromotionAgentDtos.cs`.

## Flow

```
Objective ─► Gather data (tools) ─► Draft proposal (model) ─► Price (CalculatePromotion)
          ─► Deterministic validation ─► Human approval ─► Re-validate ─► PromotionService
```

| # | Step (AgentName) | What is persisted |
|---|---|---|
| 1 | Gather data (Inventory & Promotion Agent) | 5 tool executions with arguments/results |
| 2 | Draft proposal (Inventory & Promotion Agent) | `SubmitPromotionProposal` record with the parsed proposal JSON |
| 3 | Price proposals (Inventory & Promotion Agent) | one `CalculatePromotion` per proposal |
| 4 | Validate (Deterministic Validator) | one `AgentValidationResult` per rule per proposal |
| 5 | Human approval (Human Reviewer) | `AgentApproval` (Pending → Approved / Rejected / RevisionRequested) |
| 6 | Re-validate before execution (Deterministic Validator) | fresh tool data + validation results |
| 7 | Create approved promotions (Promotion Service) | `ApprovedAction:CreatePromotions` with the created IDs |

Workflow status: `InProgress` → `AwaitingApproval` → `Completed` / `Cancelled` / `Failed`.
If no product qualifies, the workflow completes without asking for approval.

## Input contract — `POST /api/agents/inventory-promotion/workflows`

```json
{
  "objective": "Find products with declining sales and recommend suitable promotions.",
  "focus": 0,
  "analysisDays": 30,
  "maxProposals": 5,
  "maxDiscountPercent": 30
}
```

`objective` 10–500 chars; `focus` is numeric over HTTP (`0` = `DecliningSales`, `1` = `SlowMoving`);
`analysisDays` 7–90; `maxProposals` 1–10; `maxDiscountPercent` 1–50.
Staff or Administrator only.

## Output contract — what the model must return

```json
{
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
}
```

Parsing is strict. Any of the following means the whole output is rejected
(`MalformedOutput`) and the raw text is **not** stored:

- the output is not JSON
- it has unknown properties
- the `schemaVersion` is wrong
- a required field is missing
- the promotion type is not allowed
- the rationale is not 10–500 characters
- there are more than 10 proposals

## Tools (allow-listed, read-only)

| Tool | Arguments | Backed by |
|---|---|---|
| `GetSalesVelocity` | `analysisDays` 7–90, `limit` 1–100 | `IAnalyticsService.GetDemandInsightsAsync` |
| `GetInventory` | `productIds` 1–50 | `IAnalyticsService.GetInventoryStockAsync` |
| `GetActivePromotions` | none | `IPromotionService.GetPromotionsAsync` (live only) |
| `GetProductDetails` | `productIds` 1–50 | `IPromotionOfferService.GetProductPromotionsAsync` |
| `GetProductPricing` | `productIds` 1–50 | `IPromotionOfferService.GetProductPromotionsAsync` |
| `CalculatePromotion` | `productId`, `promotionType`, `discountValue`, `startDate`, `endDate` | `PromotionDiscountCalculator` (nothing saved) |

`PromotionAgentToolRegistry` enforces the following:
- **Permissions:** tools outside the allow-list are refused and logged, even when an implementation is registered.
- **Input validation:** unknown or ill-typed arguments and out-of-range values are rejected and never retried.
- **Output validation:** each result must contain its required property, within a size limit.
- **Time limits:** each call has a timeout (default 5 s).
- **Retries:** transient failures get bounded retries (default 2).
- **Redaction:** secrets are removed from any stored JSON.
- **Audit:** every attempt, success or failure, is saved as an `AgentToolExecution`.

## Validation rules (deterministic, fail closed)

Facts are rebuilt from tool data, never taken from the model.

| Rule | Checks |
|---|---|
| `ProposalLimit` | count ≤ `maxProposals`, no duplicate products |
| `ProductExists` | product exists and is active |
| `ReviewerConstraints` | product not excluded by the reviewer |
| `SufficientInventory` | available stock above the reorder level |
| `DiscountLimits` | effective discount in (0, `maxDiscountPercent`]; fixed amounts measured against the cheapest variant |
| `PromotionDates` | starts today or later, lasts 1–60 days |
| `NoConflict` | no live promotion on the product or its categories |
| `EvidenceMatchesData` | evidence equals the tool data (stops invented figures) |
| `ServerPricing` | the server priced every variant; no item becomes free |

Any failure means the workflow ends as `Failed` (`ValidationFailed`) and nothing is changed.

## Approval

| Impact | Rule | Who can approve |
|---|---|---|
| Low | every discount < 20% and ≤ 3 proposals | Staff or Administrator |
| High | any discount ≥ 20% or > 3 proposals | Administrator |

- `POST .../workflows/{id}/approve`:
  1. re-runs the tools and validation against current data (a stale proposal fails as `StaleProposal`)
  2. creates every proposed promotion **atomically** with `PromotionService.CreatePromotionsAsync`
- `POST .../workflows/{id}/reject`: sets the workflow to `Cancelled`; nothing is changed.
- `POST .../workflows/{id}/revise` `{ "comment", "maxDiscountPercent"?, "maxProposals"?, "excludeProductIds"? }`:
  - records `RevisionRequested`
  - runs a new proposal cycle under the new limits
  - is capped at 3 revisions
- Decisions are only accepted while the workflow is `AwaitingApproval`, and each approval can be decided once: approve, reject and revise claim it with a conditional `UPDATE … WHERE Status = 'Pending'` inside a transaction, so concurrent reviews cannot both act (the loser gets 409).

## Safe failure

| Error type | Cause |
|---|---|
| `ToolFailure` / `ToolTimeout` | tool failed or timed out after bounded retries |
| `ToolNotPermitted` | a non-allow-listed tool was requested |
| `MalformedOutput` | model output failed schema parsing |
| `ModelTimeout` | model exceeded its timeout (default 20 s) |
| `ValidationFailed` / `StaleProposal` | business rules failed |
| `UnexpectedError` | anything else; details go only to the server log |

Every failure is persisted (`AgentWorkflowError`, failed step, `FinalOutcome`).
Pending business changes are discarded, so nothing partial is saved.

## Model boundary

`IPromotionProposalModel` is the replaceable AI boundary. It receives only the
request and the tool results (`PromotionAgentContext`) and returns text that must
match the output contract.

The registered `LocalPromotionProposalModel` is a deterministic,
data-grounded policy, the same approach as the other agents in this project:
- **DecliningSales:** sales dropped by 10% or more vs the previous period. The discount is 10%, 15% (drop ≥ 25%) or 20% (drop ≥ 50%), capped by `maxDiscountPercent`.
- **SlowMoving:** no sales with stock available, or more than 60 days of cover.
- **Skipped products:** those with a live promotion, too little stock, or excluded by the reviewer.

An LLM-backed implementation can replace it without changing the tools,
validator or approval flow.

## Not stored

No hidden reasoning or chain-of-thought is persisted. The workflow stores only:
- the objective and the fixed plan
- tool arguments and results (redacted)
- the parsed structured proposal
- validation results, approvals, errors and the final outcome

Malformed model output is discarded.

## Configuration (`appsettings.json`, section `InventoryPromotionAgent`)

`ToolTimeout`, `ModelTimeout`, `MaxToolRetries`, `MaxRevisions`,
`HighImpactDiscountPercent`, `HighImpactProposalCount`, `MaxPromotionDays`,
`MaxModelOutputCharacters`, `MaxToolOutputCharacters`. All have defaults.

## Shared workflow integration

The agent uses the existing `AgentWorkflow`, `AgentWorkflowStep`,
`AgentToolExecution`, `AgentValidationResult`, `AgentApproval` and
`AgentWorkflowError` tables. **It needs no schema change.** The structured
proposal is stored in the existing `jsonb` tool-execution columns. The only
shared-model change is `ApprovalStatus.RevisionRequested`, which is identical
to the line added on the Product & Inventory branch.
