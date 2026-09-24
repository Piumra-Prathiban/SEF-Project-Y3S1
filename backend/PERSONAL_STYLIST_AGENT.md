# Personal Stylist Agent

## Responsibility

The Personal Stylist Agent is a fashion-shopping recommendation workflow. It uses the authenticated customer's profile and wishlist together with the current catalogue and inventory to recommend up to five available product variants for an occasion and budget. It is not a general chatbot and cannot perform write operations.

The authenticated client starts the workflow with `POST /api/recommendations`. Customer identity always comes from the JWT; prices, product details, variants, and stock always come from backend services.

## Request contract

```json
{
  "occasion": "Wedding",
  "budget": 20000,
  "preferredColours": ["Navy"],
  "preferredSize": "M",
  "stylePreferences": "Classic"
}
```

- `occasion` is required and must contain 2–100 characters.
- `budget`, when supplied, must be positive and limits the authoritative total
  price of all recommended quantities.
- Up to 10 nonblank colours may be supplied; each is limited to 50 characters.
- Size is limited to 50 characters and style preferences to 500 characters.
- Inputs are normalized at the API boundary and validated again at each tool boundary.

## Response contract

```json
{
  "workflowId": "00000000-0000-0000-0000-000000000000",
  "status": "completed",
  "recommendations": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "variantId": "00000000-0000-0000-0000-000000000000",
      "productName": "Formal Jacket",
      "variantName": "Medium",
      "sku": "FORMAL-M",
      "price": 15000,
      "quantity": 1,
      "availableQuantity": 3,
      "size": "M",
      "colour": "Navy",
      "reason": "Matches the occasion and is in your wishlist."
    }
  ],
  "execution": {
    "agentName": "Personal Stylist Agent",
    "status": "completed",
    "toolAttempts": 4,
    "successfulToolExecutions": 4,
    "outputValidated": true,
    "errorSummary": null,
    "validationResults": [
      {
        "rule": "Price",
        "isValid": true,
        "message": "Every recommended price matches authoritative catalogue data."
      }
    ]
  }
}
```

Every model proposal must include product ID, variant ID, price, quantity,
optional structured size/colour, and a reason. It then passes deterministic
checks for schema, product existence/activity, variant existence/ownership,
size, colour, availability, stock, authoritative price, and total budget. No
unsafe claim is silently repaired. A failure rejects the complete proposal and
returns an empty `recommendations` array with the failed checks in the execution
summary.

## Allow-listed tools

The agent can call only these read-only tools:

1. `customer_preference` — retrieves the authenticated customer's profile through `IProfileService`.
2. `wishlist` — retrieves that customer's wishlist through `IWishlistService`.
3. `product_search` — performs server-side catalogue search through `IProductSearchService`.
4. `product_availability` — rechecks active product/variant state and available inventory through `IProductAvailabilityService`.

The agent depends on typed tool interfaces and never receives `AppDbContext`. The tools call existing application/domain services; the product-availability domain service is the only new catalogue query boundary. Orders, inventory, promotions, payments, and other state cannot be modified.

The current catalogue model does not expose structured size or colour fields.
These preferences are reported in `unappliedPreferences`, and validation fails
closed if a recommendation is produced for a requested size or colour that
cannot be authoritatively verified. The interfaces remain ready for those
capabilities when Member 1's schema supplies them.

## Reliability and audit behaviour

- Overall and per-operation timeouts are configurable in `PersonalStylistAgent` settings.
- Controlled tool calls have a bounded retry limit; only transient failures are retried.
- Tool, timeout, malformed-output, and unexpected failures return no recommendations.
- Each workflow persists its agent step, tool name/status, every deterministic
  validation result, safe error summary, and final grounded IDs in the existing
  agent workflow tables.
- Tool summaries omit names, free-form preferences, and hidden reasoning. No chain-of-thought is recorded.

`IPersonalStylistRecommendationModel` is the replaceable model boundary. The registered implementation uses a deterministic, catalogue-grounded selection policy, so the feature remains safe and testable without giving a model direct database access. Any future AI provider implementation must retain the same structured output contract and final grounding validator.
