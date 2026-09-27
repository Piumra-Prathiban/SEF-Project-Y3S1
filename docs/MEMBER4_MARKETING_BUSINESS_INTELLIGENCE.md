# Member 4 — Marketing & Business Intelligence

## Delivery status

Member 4 is implemented across the ASP.NET Core API, PostgreSQL/EF Core,
React staff dashboard, customer storefront, Flutter customer app, automated
tests, and the Inventory & Promotion Agent workflow.

| Area | Status | Main implementation |
| --- | --- | --- |
| Promotions | Complete | Staff CRUD, product/category targeting, campaign assignment, live-customer visibility, server-calculated offers, active date/state checks and delete safeguards |
| Campaigns | Complete | Staff CRUD, draft/active/completed lifecycle, date validation and promotion membership rules |
| Customer pricing | Complete | Checkout applies the best eligible live price promotion, stores gross/discount/net totals, snapshots the paid unit price and creates the pending payment for the net total |
| Coupons | Complete for checkout | Codes are normalized, validated for date/state/global usage/per-customer usage/product eligibility, and persisted as auditable `CouponRedemption` rows |
| Dashboard | Complete | Sales KPIs, revenue trend, top products, stock summary and live promotion counts |
| Analytics | Complete | Sales over time, product performance, inventory position, promotion performance and date filters |
| Reports | Complete | Demand insights with movement/trend evidence, reorder context and promotion context |
| Inventory & Promotion Agent | Complete | Distinct input/output contract, six allow-listed tools, persisted workflow state, strict structured output, deterministic validation, impact-based approval and audited promotion creation |
| Flutter promotions | Complete | Active deals list, promotion detail, eligible products, API-priced variants, error/empty/retry states and a reachable Shop action |

## React pages

The pages below require a `Staff` or `Administrator` account.

- `/marketing` — marketing overview
- `/marketing/dashboard` — executive KPI dashboard
- `/marketing/analytics` — sales, product, inventory and promotion analytics
- `/marketing/reports` — demand insights and operational report
- `/marketing/promotions` — promotions list
- `/marketing/promotions/new` — create promotion
- `/marketing/promotions/:id` — promotion detail and lifecycle actions
- `/marketing/promotions/:id/edit` — edit promotion
- `/marketing/campaigns` — campaigns list
- `/marketing/campaigns/new` — create campaign
- `/marketing/campaigns/:id` — campaign detail
- `/marketing/campaigns/:id/edit` — edit campaign
- `/marketing/agent` — Inventory & Promotion Agent workflows
- `/marketing/agent/new` — start an evidence-gathering workflow
- `/marketing/agent/:id` — proposal, tools, validation, approval and audit detail

Customer-facing routes remain `/`, `/shop/:id`, `/cart` and `/checkout`.
Product detail now requests the public product-promotion projection and shows
the API's original/final price. Checkout sends the optional coupon code and
uses the server response as the final price authority.

## Backend endpoints

### Promotions

- `GET /api/promotions` and `GET /api/promotions/{id}` — live promotions are public; staff can also see inactive/upcoming records
- `GET /api/promotions/{id}/products` — eligible products with authoritative offer prices
- `GET /api/promotions/products/{productId}` — live promotions and best offer for every variant
- `GET /api/promotions/targets` — staff product/category target options
- `POST`, `PUT`, `DELETE /api/promotions` — staff administration
- `POST /api/promotions/{id}/calculate-discount` — validated server-side calculation

### Campaigns and analytics

- `GET`, `POST`, `PUT`, `DELETE /api/campaigns`
- `GET /api/analytics/sales/summary`
- `GET /api/analytics/sales/over-time`
- `GET /api/analytics/products/performance`
- `GET /api/analytics/inventory/stock`
- `GET /api/analytics/inventory/summary`
- `GET /api/analytics/promotions/performance`
- `GET /api/analytics/demand`

### Agent workflow

- `POST /api/agents/inventory-promotion/workflows`
- `GET /api/agents/inventory-promotion/workflows`
- `GET /api/agents/inventory-promotion/workflows/{id}`
- `POST /api/agents/inventory-promotion/workflows/{id}/approve`
- `POST /api/agents/inventory-promotion/workflows/{id}/reject`
- `POST /api/agents/inventory-promotion/workflows/{id}/revise`

The agent itself cannot write promotions. It can only call its allow-listed
read/calculation tools. A valid proposal pauses for a staff or administrator
review; high-impact proposals require an administrator. Approval re-runs
validation against current database facts and then delegates the write to
`PromotionService`.

## Real model configuration

Local development uses `LocalPromotionProposalModel` so the workflow remains
repeatable without an external account. To enable the real model-backed agent,
add these values to the gitignored backend `.env`:

```dotenv
InventoryPromotionAgent__Provider=OpenAI
InventoryPromotionAgent__Model=gpt-5-mini
InventoryPromotionAgent__ApiKey=YOUR_OPENAI_API_KEY
```

`OpenAiPromotionProposalModel` sends only structured tool results to the
Responses API with `store: false` and a strict JSON Schema. The response is
still untrusted: it passes the existing strict parser, server-side price tool,
business-rule validator and human approval before any promotion is created.
The API shape follows the official OpenAI Structured Outputs guidance:
<https://developers.openai.com/api/docs/guides/structured-outputs>.

## Verification

Verified on 2026-09-26:

- Backend: `637` tests passed, including the demo seed/reset coverage added after the Member 4 audit.
- React: `355` tests passed; ESLint passed; Vite production build passed.
- Flutter: `85` tests passed; `flutter analyze` reported no issues.
