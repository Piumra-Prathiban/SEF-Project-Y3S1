# Agentic AI workflow plan

This document records the proposed agents for Clothic and the implementation direction discussed for the project assessment. It is a design and status note, not a claim that the complete four-agent workflow already works.

## Goal

Demonstrate at least one end-to-end fashion-commerce workflow that accepts a customer or staff objective, creates a structured plan, delegates work to distinct agents, uses controlled tools, validates results, pauses high-impact actions for human approval, and persists an auditable outcome or safe failure.

The assessed agents must have different responsibilities, typed input/output contracts, separate tool permissions, and visible participation in the same workflow. Four differently named prompts or four fixed `if/else` steps would not satisfy that intent.

## Proposed four agents

| Agent | Owns | Example inputs | Allowed capabilities | Structured output |
| --- | --- | --- | --- | --- |
| **1. Planning / Coordinator** | Interpret the objective, choose relevant specialists, order tasks, and revise the plan when evidence changes. | Objective, budget, occasion, workflow context, prior step results. | Invoke only registered specialist agents; read workflow state. No direct catalogue or business writes. | Plan with ordered tasks, assigned agent, rationale, required evidence, and completion criteria. |
| **2. Personal Stylist** | Build and explain an outfit from real, available product variants. | Occasion, preferences, sizes, colours, budget, customer context. | Read customer preferences, wishlist, product search, and authoritative variant availability. No writes. | Product/variant IDs, proposed outfit, prices, reasons, and unmet preferences. |
| **3. Inventory & Promotion** | Investigate stock, sales velocity, prices, and existing promotions; propose a substitution, restock, or bounded promotion when warranted. | Candidate variants, sales and stock context, promotion limits. | Read inventory, sales, product pricing, and live promotions; calculate a proposed discount. No direct inventory or promotion writes. | Evidence-backed proposal, alternatives, expected impact, and approval requirement. |
| **4. Validation & Business Rules** | Critique the combined proposal, identify unsupported claims, request more evidence or a revision, and explain failures. | Plan, agent outputs, tool evidence, current catalogue and policy facts. | Read-only verification tools and validation results. No business writes or approval authority. | Pass/revise/reject recommendation with cited evidence and explicit issues. |

The fourth agent should perform an **independent model-backed review** to count as a distinct AI participant. Deterministic code remains the final authority for product existence, variant ownership, stock, price, budget, discount limits, authorization, and permitted state transitions. The reviewer cannot override a failed rule.

The coordinator must make a genuine planning or delegation decision from the objective and later evidence. A hard-coded plan displayed under a coordinator name is insufficient on its own. Similarly, the stylist and promotion agent should compare grounded options and explain choices rather than only apply fixed thresholds.

## Example assessed workflow

1. A customer asks for a weekend smart-casual outfit within a budget and selects a size and preferred colours.
2. The Coordinator persists the objective and a structured plan, then delegates product discovery to the Stylist.
3. The Stylist searches real catalogue variants and proposes an outfit with product and variant IDs.
4. The Inventory & Promotion agent checks availability, sales, prices, and any existing promotions. If a proposed size is unavailable, it supplies evidence and an alternative. It may propose a promotion only within configured limits.
5. The Coordinator revises or delegates again if the alternative changes the plan.
6. The Validation agent challenges unsupported claims and requests revision when needed. Server-side deterministic checks independently verify every ID, price, stock quantity, budget, and promotion rule against current data.
7. A proposed discount or other high-impact business change pauses for an authorized staff member. Approval, rejection, or revision is recorded; approval triggers a fresh validation before execution through the normal domain service.
8. The workflow stores its final recommendation, approved action, or safe failure with ordered agent steps, tool calls, validation results, errors, timings, and approval decisions under one workflow ID.

The model should never be trusted to invent catalogue items, declare a payment successful, change stock, or set prices by itself. Tool arguments and outputs need validation, allow-lists, timeouts, bounded retries, role checks, and safe error handling.

## What exists in this repository today

- **Personal Stylist:** `backend/SEF_Project.Api/Services/Recommendations/` has read-only tools, a replaceable `IPersonalStylistRecommendationModel`, grounding checks, and workflow recording. The registered `GroundedPersonalStylistModel` is a deterministic local policy, not an external AI model. `POST /api/recommendations` starts its current standalone workflow.
- **Inventory analysis:** `backend/SEF_Project.Api/Services/AgenticAI/` has controlled inventory tools, recommendation validation, persisted workflow steps, and staff approval. The registered `LocalInventoryAnalysisModelClient` is deterministic.
- **Inventory & Promotion:** `backend/SEF_Project.Api/AI/InventoryPromotion/` has read-only tools, structured proposals, deterministic validation, approval/revision, and a replaceable `IPromotionProposalModel`. The registered `LocalPromotionProposalModel` is deterministic. Its workflows start at `POST /api/agents/inventory-promotion/workflows`.
- **Shared records:** the backend already has `AgentWorkflow`, `AgentWorkflowStep`, tool-execution, validation, error, and approval entities. These are a foundation for a combined workflow.
- **Coordinator and validator gap:** the stylist workflow records a coordinator step and a fixed plan, and existing workflows run deterministic validators. A model-backed coordinator that adapts/delegates across all four roles, an independent model-backed reviewer, and **one integrated assessed workflow** are still to be implemented.

The existing local policies are useful test baselines and safe fallbacks. They should not be presented as evidence that four model-backed AI agents already collaborate.

## Implementation approach in the existing stack

The project does **not** need Python or LangChain. Keep orchestration in the ASP.NET Core backend and the user experience in React/Flutter. Use one model-provider API key stored only in backend configuration, never in either client. The official [OpenAI .NET SDK](https://github.com/openai/openai-dotnet) can call the [Responses API](https://developers.openai.com/api/docs/quickstart); [function calling](https://developers.openai.com/api/docs/guides/function-calling) and [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) support controlled tool requests and typed plans/proposals. A .NET agent framework is optional, not a prerequisite.

1. Define versioned contracts for each agent's inputs, outputs, permitted tools, and failure states.
2. Add a model-provider abstraction in the backend and configure a real provider for planning, styling/proposal judgment, and independent review. Keep existing deterministic implementations for tests and offline fallback.
3. Build a coordinator service that creates a variable plan, invokes specialists through their contracts, responds to revision requests, and persists every transition in one workflow.
4. Apply existing deterministic domain validators after model output and again immediately before any approved business write.
5. Expose a customer/staff workflow view that shows the objective, plan, agent steps, evidence, validation, and approval state. Do not display hidden model reasoning or secrets.
6. Test successful runs and failures: unavailable size, invented product ID, stale stock, over-budget outfit, excessive discount, tool timeout, malformed output, unauthorized approval, rejection, and revision.

## Optional additional agent

**Fulfilment Exception Agent** is the strongest distinct addition after the required four work together. The orders domain already provides order, payment, shipment, and status-history data. This agent could investigate a stalled order, identify a likely blocker, compare permitted next steps, and propose a staff-reviewed resolution. It must not directly mark payments complete, shipments delivered, or orders cancelled. Its tools and contract would be separate from catalogue, styling, and promotion tools.

A Returns & Exchange Agent could be valuable later, after the deferred return entities and workflow exist. A Size & Fit Agent should wait until the catalogue contains enough measurements or fit data to ground its advice.

If the assessment requires the four named roles exactly, keep and strengthen them. Replacing one with another role should follow the project plan's requirement for written lecturer approval.

## Evidence of completion

The feature is ready to demonstrate when a reviewer can submit an objective and inspect **one workflow ID** showing a non-fixed plan, distinct agent handoffs and tool permissions, grounded product IDs, independent validation, an approval pause for a high-impact action, and a final result or recorded safe failure. Tests and a live demonstration should show the agents changing course when stock, budget, or business-rule evidence contradicts an earlier proposal.
