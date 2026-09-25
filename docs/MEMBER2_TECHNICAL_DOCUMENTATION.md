# Member 2: Shopping and Customer Experience

## 1. Scope and ownership

Member 2 owns customer-facing shopping discovery, wishlist, cart, profile and
address management, and the Personal Stylist recommendation workflow. The
component includes ASP.NET Core APIs, EF Core persistence, a React web client,
and a Flutter mobile client.

The component consumes, but does not own:

- Product, ProductVariant, Category, Size, Colour, Collection, and inventory
  data owned by Member 1.
- Checkout, orders, payment, fulfilment, cancellation, and returns owned by
  Member 3.
- Promotions, effective pricing, and analytics owned by Member 4.
- User identity, JWT issuance, and roles provided by shared authentication.

The main execution path is:

```text
React or Flutter
    -> ASP.NET Core controllers and request DTOs
    -> Member 2 application services
    -> shared EF Core AppDbContext
    -> PostgreSQL

Recommendation request
    -> RecommendationService
    -> persisted workflow/coordinator step
    -> Personal Stylist Agent
    -> allow-listed read-only tools
    -> deterministic output validation
    -> structured response or safe failure
```

## 2. Database design

All entities use the shared `AppDbContext`. Auditable entities receive
`CreatedAt` and `UpdatedAt` values in `SaveChangesAsync`.

| Entity | Key | Purpose | Important constraints |
|---|---|---|---|
| Customer | `int Id` | Existing customer profile linked to the shared User | One-to-one User relationship |
| Address | `int Id` | Customer delivery/profile address | Required fields validated; ownership enforced in queries |
| Wishlist | `Guid Id` | One wishlist per customer | Unique `CustomerId` |
| WishlistItem | `Guid Id` | Saved catalogue product | Unique `(WishlistId, ProductId)` |
| Cart | `Guid Id` | One active cart per customer | Unique `CustomerId` |
| CartItem | `Guid Id` | Selected product variant and quantity | Unique `(CartId, ProductVariantId)` and `Quantity > 0` check |
| AgentWorkflow | `Guid Id` | Persisted objective, plan, state, and outcome | Indexed by status and creation time |
| AgentWorkflowStep | `Guid Id` | Ordered agent participation | Unique `(WorkflowId, StepOrder)` |
| AgentToolExecution | `Guid Id` | Structured tool execution summary | JSONB arguments/results; linked to a workflow step |
| AgentValidationResult | `Guid Id` | Deterministic validation evidence | Linked to a workflow step |
| AgentWorkflowError | `Guid Id` | Sanitized workflow failure | Linked to workflow and optionally a step |

### Relationships

```text
User 1 -- 0..1 Customer
Customer 1 -- * Address
Customer 1 -- 0..1 Wishlist 1 -- * WishlistItem * -- 1 Product
Customer 1 -- 0..1 Cart     1 -- * CartItem     * -- 1 ProductVariant

AgentWorkflow 1 -- * AgentWorkflowStep
AgentWorkflowStep 1 -- * AgentToolExecution
AgentWorkflowStep 1 -- * AgentValidationResult
AgentWorkflow 1 -- * AgentWorkflowError
```

Customer-owned dependants cascade when their owner is deleted. Catalogue
references use restricted deletion so a product or variant cannot be removed
while a wishlist or cart still references it.

Member 2's shopping migration is
`20260923070015_AddShoppingDatabaseFoundation`. Agent workflow tables came from
the shared domain migration and are reused rather than duplicated.

## 3. API endpoint reference

The development OpenAPI document is available at `/swagger/v1/swagger.json`
and Swagger UI at `/swagger`. Swagger reads controller XML comments, response
types, validation annotations, and the configured JWT Bearer scheme. Use the
access token returned by shared authentication in Swagger's **Authorize**
dialog.

| Method | Route | Purpose | Authentication | Owner |
|---|---|---|---|---|
| GET | `/api/shopping/products` | Search, filter, sort, and page active products | Anonymous | Member 2 |
| GET | `/api/wishlist` | Get the current customer's wishlist | JWT customer | Member 2 |
| POST | `/api/wishlist/items` | Add a product to the wishlist | JWT customer | Member 2 |
| DELETE | `/api/wishlist/items/{productId}` | Remove an owned wishlist product | JWT customer | Member 2 |
| GET | `/api/wishlist/count` | Get wishlist item count | JWT customer | Member 2 |
| GET | `/api/cart` | Get the current customer's cart and totals | JWT customer | Member 2 |
| POST | `/api/cart/items` | Add a variant or increase its existing quantity | JWT customer | Member 2 |
| PUT | `/api/cart/items/{id}` | Replace an owned cart item's quantity | JWT customer | Member 2 |
| DELETE | `/api/cart/items/{id}` | Remove an owned cart item | JWT customer | Member 2 |
| DELETE | `/api/cart` | Clear the current customer's cart | JWT customer | Member 2 |
| GET | `/api/profile` | Get the current customer's profile | JWT customer | Member 2 |
| PUT | `/api/profile` | Update existing shared User profile fields | JWT customer | Member 2 |
| GET | `/api/profile/addresses` | List owned addresses | JWT customer | Member 2 |
| POST | `/api/profile/addresses` | Create an address | JWT customer | Member 2 |
| PUT | `/api/profile/addresses/{id}` | Update an owned address | JWT customer | Member 2 |
| DELETE | `/api/profile/addresses/{id}` | Delete an owned address | JWT customer | Member 2 |
| POST | `/api/recommendations` | Run the fashion recommendation workflow | JWT customer | Member 2 |
| POST | `/api/Auth/login` | Obtain the shared JWT used by both clients | Anonymous | Shared Auth |
| POST | `/api/Orders` | Create an order from authoritative variant/quantity data | JWT customer | Member 3 |

### Product search and filtering

`GET /api/shopping/products` builds an EF Core `IQueryable`; filtering,
sorting, counting, `Skip`, and `Take` execute in the database. The catalogue is
not loaded before filtering.

Current query parameters are:

- `search`: case-insensitive match against product name/description and active
  variant name/SKU; maximum 100 characters.
- `categoryId`: requires membership in an active category.
- `minPrice` and `maxPrice`: inclusive active-variant price bounds; nonnegative
  and minimum cannot exceed maximum.
- `inStockOnly`: requires `QuantityOnHand - ReservedQuantity > 0`.
- `sortBy`: allow-listed values `name`, `price`, or `newest`.
- `sortDirection`: `asc` or `desc`.
- `page`: 1 through 10,000.
- `pageSize`: 1 through 100.

Inactive products and products without an active variant are excluded. Results
contain page metadata, active variants, current available quantity, and minimum
active-variant price. A stable product-ID tie-breaker keeps pagination
deterministic.

Collection, size, and colour filters are intentionally not implemented against
placeholder entities. They require Member 1's latest catalogue schema to be
integrated first.

### Wishlist rules

- Identity is obtained only from the JWT `NameIdentifier` claim.
- Only an active catalogue product can be added.
- The database unique index and service both prevent duplicate products.
- Every read and mutation is scoped through the authenticated customer.
- Removing an absent or another customer's item returns `404` without exposing
  whether another customer owns it.
- API responses use DTOs rather than EF entities.

### Cart rules

- Inputs contain only `productVariantId` and a positive `quantity`.
- Product name, SKU, price, activity, stock, subtotals, and totals are read or
  calculated by the backend.
- A variant and its product must be active.
- Available quantity is `QuantityOnHand - ReservedQuantity`.
- Adding an existing variant increases the existing row instead of duplicating
  it; the unique index is a second safeguard.
- Adding to cart does not reserve stock. Member 3's checkout revalidates and
  reserves inventory transactionally.
- All item update/delete queries include customer ownership.
- Checkout, payment, and order creation are outside Member 2's service.

### Profile and address rules

- Profile updates reuse shared User email, first name, and last name fields.
- Email is normalized and remains globally unique.
- Required strings cannot be whitespace-only.
- Address length and postal-code format are validated at the DTO and service
  boundaries.
- The first address automatically becomes default.
- Selecting a new default unsets the previous default in the same save.
- Deleting the default selects the oldest remaining address as replacement.
- Address lookup, update, and deletion always include `CustomerId` ownership.

## 4. Recommendation architecture

The recommendation endpoint is fashion-specific and does not accept general
chat messages. The client supplies preferences; customer identity, profile,
wishlist, catalogue data, prices, and availability are retrieved server-side.

| Agent | Responsibility | Inputs | Outputs | Tools | Validation |
|---|---|---|---|---|---|
| Recommendation Workflow Coordinator | Persist the objective and structured plan, delegate the stylist step, and preserve workflow status | Authenticated user ID and normalized recommendation context | Workflow ID, plan/step state, final status | Delegates to Personal Stylist Agent | Requires persisted objective and plan before delegated execution |
| Personal Stylist Agent | Select up to five fashion variants grounded in the controlled catalogue for the requested occasion and budget | Customer context, occasion, budget, colours, size, style preferences, wishlist, catalogue candidates | Validated product/variant IDs, authoritative display data, reasons, execution summary, or safe failure | `customer_preference`, `wishlist`, `product_search`, `product_availability` | Schema, product, variant ownership, size, colour, activity, availability, stock, price, and budget |

### Controlled tools

| Tool | Input | Structured output | Access boundary |
|---|---|---|---|
| `customer_preference` | User ID and requested preferences | Customer context and normalized preferences | Calls `IProfileService`; no direct agent database access |
| `wishlist` | User ID | Owned saved-product summaries | Calls `IWishlistService` |
| `product_search` | Search text, maximum price, result limit | Catalogue products, available variants, and capability flags | Calls `IProductSearchService` |
| `product_availability` | Product/variant ID pairs | Current authoritative price, availability, stock, size, and colour | Calls `IProductAvailabilityService` |

Only these exact tool names are allow-listed. Tool inputs have independent
validation. Tools are read-only and cannot modify carts, wishlists, orders,
inventory, payments, or promotions.

### Public request contract

The following is an illustrative request, not a record of a production call:

```json
{
  "occasion": "Dinner",
  "budget": 20000,
  "preferredColours": ["Black", "White"],
  "preferredSize": "M",
  "stylePreferences": "Smart casual"
}
```

The response contains:

```json
{
  "workflowId": "00000000-0000-0000-0000-000000000000",
  "status": "completed",
  "recommendations": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "variantId": "00000000-0000-0000-0000-000000000000",
      "productName": "Catalogue product name",
      "variantName": "Catalogue variant name",
      "sku": "CATALOGUE-SKU",
      "price": 15000,
      "quantity": 1,
      "availableQuantity": 3,
      "size": "M",
      "colour": "Black",
      "reason": "Short fashion recommendation reason"
    }
  ],
  "execution": {
    "agentName": "Personal Stylist Agent",
    "status": "completed",
    "toolAttempts": 4,
    "successfulToolExecutions": 4,
    "outputValidated": true,
    "errorSummary": null,
    "validationResults": []
  },
  "unappliedPreferences": [],
  "relaxedCriteria": []
}
```

Names, prices, stock, size, and colour in a real response are populated only
from the controlled catalogue and availability outputs.

### Deterministic validation and safe failure

The agent output is untrusted until all checks pass:

1. Output schema and recommendation-count limit.
2. Product exists in the controlled result and is active.
3. Variant exists and belongs to the stated product.
4. Claimed and requested size match authoritative data.
5. Claimed and requested colour match authoritative data.
6. Variant is active and available.
7. Quantity is positive and stock is sufficient.
8. Claimed price exactly matches authoritative price.
9. Authoritative total respects the budget.

The validator rejects the complete proposal if a check fails; it never silently
changes an unsafe result. Timeout, malformed output, rejected tools, and tool
failure return an empty recommendation list with a sanitized execution summary.
Overall timeout, per-tool timeout, and retry count are bounded by configuration
(defaults: 20 seconds, 5 seconds, and 2 attempts; maximum retry attempts: 3).

Persisted audit state contains workflow IDs, objectives, plans, named agent
steps, tool names and statuses, validation results, safe error summaries, and
the final outcome. It does not store hidden reasoning or chain-of-thought.

## 5. Validation and error handling

ASP.NET Core model validation handles Data Annotation failures before service
execution. Services repeat important business validation so rules also apply
when called outside controllers.

The global exception handler returns RFC-style `application/problem+json`:

| Condition | Status |
|---|---:|
| Invalid DTO or business argument | 400 |
| Missing/invalid authenticated customer | 401 |
| Owned resource or catalogue item not found | 404 |
| Duplicate, unavailable item, stock conflict, or duplicate email | 409 |
| Unexpected exception | 500 with a generic client-safe message |

React and Flutter convert Problem Details responses into user-facing error
states. Loading, retry, empty, validation, timeout, and safe-failure states are
represented explicitly where applicable.

## 6. Security

- JWT validation checks issuer, audience, lifetime, signing key, and signature.
- Customer endpoints use `[Authorize]`; product discovery alone is currently
  `[AllowAnonymous]`.
- Customer identity is never accepted in request bodies or query strings.
- Ownership is enforced in database predicates, not only in the UI.
- Request DTOs exclude authoritative prices, stock, totals, and product names.
- Recommendation tools are allow-listed and read-only; the agent has no
  `AppDbContext` dependency.
- Deterministic validation prevents fabricated products, variants, prices, and
  stock from reaching the response.
- CORS uses configured explicit origins rather than wildcard origins.
- Unexpected errors do not return stack traces or internal exception details.
- Flutter stores tokens through `flutter_secure_storage`.
- React currently stores its session in `localStorage`; this is documented as a
  residual browser-XSS risk and should be replaced by an agreed shared web-auth
  strategy if the team introduces one.

## 7. React implementation

The React application uses React Router and a shared API wrapper. Public product
browsing lives at `/products`; `/wishlist`, `/cart`, and `/profile` use a
`ProtectedRoute`. Server calls are in `shoppingService.js`; search and filters
are sent as API query parameters rather than applied to a downloaded catalogue.

Reusable UI includes the application shell, protected route, product card,
pagination, address form, and loading/error/empty state components. Cart totals
are rendered from the backend response. React deliberately does not implement
checkout or recommendation UI owned elsewhere in the current phase split.

## 8. Flutter implementation

Flutter uses `CustomerStore` (`ChangeNotifier`) for feature state,
`CustomerRepository` as the API boundary, `http` for requests, and secure token
storage. `HomeShell` provides Shop, Stylist, Wishlist, Cart, and Profile
destinations.

The app supports server-side product discovery, product details and variant
selection, wishlist actions, cart updates, profile/address forms, and the full
recommendation status lifecycle: pending, processing, completed, validation
failure, safe failure, timeout, and API error. Recommendation actions call the
normal wishlist/cart APIs; agent output never mutates customer data directly.

## 9. API examples

These commands are templates and are not execution evidence. Replace IDs,
tokens, and the base URL with values from the target environment.

```bash
# Public database-side discovery
curl "http://localhost:5193/api/shopping/products?search=jacket&minPrice=5000&maxPrice=20000&inStockOnly=true&sortBy=price&sortDirection=asc&page=1&pageSize=12"

# Add an authoritative product variant to the cart
curl -X POST "http://localhost:5193/api/cart/items" \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d '{"productVariantId":"<VARIANT_GUID>","quantity":1}'

# Save a product
curl -X POST "http://localhost:5193/api/wishlist/items" \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d '{"productId":"<PRODUCT_GUID>"}'

# Start the Personal Stylist workflow
curl -X POST "http://localhost:5193/api/recommendations" \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d '{"occasion":"Dinner","budget":20000,"preferredColours":["Black"],"preferredSize":"M","stylePreferences":"Smart casual"}'
```

## 10. Testing strategy and verified results

Backend tests use xUnit and SQLite in-memory relational databases. They cover
controller authorization, service ownership, DTO validation, EF relationships
and constraints, discovery query behavior, wishlist/cart/profile behavior,
workflow persistence, tool allow-listing, retry/timeout/safe failure, malformed
agent output, deterministic recommendation validation, and cart-to-order
integration.

React uses Vitest and Testing Library for protected routes, API integration,
search, wishlist, cart, profile, and loading/error/empty states. Flutter uses
unit and widget tests for its API client, store, navigation, shopping screens,
and recommendation states.

Verified on 2026-09-24 from the Phase 17 working tree based on commit
`a7aad98`:

| Check | Result |
|---|---|
| `dotnet test backend/SEF_Project.Api.Tests/SEF_Project.Api.Tests.csproj -c Release --no-restore` | 214 passed, 0 failed, 0 skipped |
| `npm test` | 11 passed, 0 failed |
| `npm run lint` | Passed |
| `npm run build` | Passed |
| `flutter analyze` | No issues |
| `flutter test` | 20 passed, 0 failed |
| Development Swagger JSON smoke check | Bearer scheme present; protected cart endpoint secured; anonymous product endpoint unsecured |

The database tests validate relational behavior using SQLite. A disposable
PostgreSQL end-to-end run remains necessary after Member 1 and Member 4 schema
migrations are integrated.

## 11. Cross-component integration points

| Component | Current integration | Required coordination |
|---|---|---|
| Member 1 Product/Inventory | Member 2 references Product and ProductVariant IDs and derives availability from inventory | Latest branch changes to single Category, Collection, Size, Colour, and `InventoryStock` require Member 2 query/navigation adaptation after merge |
| Member 3 Orders | Order backend is present; checkout accepts variant ID/quantity and revalidates price and stock | A checkout screen/route and post-success cart-clearing policy are not yet supplied |
| Member 4 Marketing | No duplicate promotion calculation exists; current APIs use catalogue base price | Effective-pricing service and promotion precedence are not yet available |
| Shared Auth | Both clients use shared login/JWT; APIs derive user ID from `NameIdentifier` | Team should confirm whether catalogue discovery stays anonymous |
| Shared Agent Workflow | Existing workflow entities are reused and Personal Stylist participation is named and persisted | Downstream Inventory/Promotion and Validation agents remain owned by their respective members |

## 12. Known limitations

1. Member 1's latest catalogue branch is not integrated. Current discovery
   cannot authoritatively filter by collection, size, or colour; requested size
   or colour recommendations fail closed or are reported as unapplied.
2. Member 3 has no checkout route in the latest frontend branch. Member 2 only
   exposes the cart handoff data and does not duplicate checkout.
3. Member 4 has not supplied a shared effective-pricing service. Displayed and
   validated prices are current catalogue base prices.
4. React has no Personal Stylist screen; the customer recommendation experience
   is currently implemented in Flutter.
5. Product details in Flutter are populated from the discovery response because
   there is no dedicated Member 2 product-details endpoint.
6. The registered recommendation model is deterministic and catalogue-grounded.
   A future external model may replace it only behind the same controlled
   interface, tool restrictions, and deterministic validator.
7. React session storage uses `localStorage` and therefore depends on strong XSS
   prevention until a shared cookie-based web-auth design is adopted.
8. Only-one-default-address behavior is enforced by the service. Concurrent
   default-address writes do not yet have a database-level partial unique index.

## 13. Contribution evidence

Evidence below is limited to artifacts verifiable from the repository and the
public GitHub metadata checked on 2026-09-24.

### Key source artifacts

| Area | Repository evidence |
|---|---|
| API controllers | [ShoppingProductsController](../backend/SEF_Project.Api/Controllers/ShoppingProductsController.cs), [WishlistController](../backend/SEF_Project.Api/Controllers/WishlistController.cs), [CartController](../backend/SEF_Project.Api/Controllers/CartController.cs), [ProfileController](../backend/SEF_Project.Api/Controllers/ProfileController.cs), [RecommendationsController](../backend/SEF_Project.Api/Controllers/RecommendationsController.cs) |
| Database | [Shopping entities](../backend/SEF_Project.Api/Models/Shopping), [shopping configuration](../backend/SEF_Project.Api/Data/Configurations/ShoppingConfigurations.cs), [migration](../backend/SEF_Project.Api/Migrations/20260923070015_AddShoppingDatabaseFoundation.cs) |
| Application services | [Shopping services](../backend/SEF_Project.Api/Services/Shopping), [profile service](../backend/SEF_Project.Api/Services/Profile/ProfileService.cs), [recommendation services](../backend/SEF_Project.Api/Services/Recommendations) |
| Agent contract | [Personal Stylist documentation](../backend/PERSONAL_STYLIST_AGENT.md), [typed contracts](../backend/SEF_Project.Api/Services/Recommendations/PersonalStylistContracts.cs), [validator](../backend/SEF_Project.Api/Services/Recommendations/PersonalStylistOutputValidator.cs) |
| Backend tests | [API test project](../backend/SEF_Project.Api.Tests), including [cross-component integration](../backend/SEF_Project.Api.Tests/CrossComponentIntegrationTests.cs) |
| React | [routes](../frontend/SEF-Project/src/App.jsx), [API service](../frontend/SEF-Project/src/services/shoppingService.js), [tests](../frontend/SEF-Project/src/test/ShoppingExperience.test.jsx) |
| Flutter | [app shell](../mobile/sef_project_mobile/sef_project/lib/screens/home_shell.dart), [state store](../mobile/sef_project_mobile/sef_project/lib/state/customer_store.dart), [API client](../mobile/sef_project_mobile/sef_project/lib/services/customer_api.dart), [tests](../mobile/sef_project_mobile/sef_project/test) |

### Branch and commits

- Branch: `feature-Shopping-&-Customer-Experience`
- Upstream: `origin/feature-Shopping-&-Customer-Experience`
- Evidence commit at the start of Phase 17: `a7aad98`
- Commit author: `Chamathka <235232193+dewnethmi11@users.noreply.github.com>`
- Remote branch: [GitHub branch](https://github.com/Piumra-Prathiban/SEF-Project-Y3S1/tree/feature-Shopping-%26-Customer-Experience)

| Commit | Evidence |
|---|---|
| `f68f154` | Shopping database foundation for wishlist and cart |
| `231378c` | Product discovery API and tests |
| `a485a17` | Authenticated wishlist backend |
| `0e54a24` | Shopping cart backend |
| `825916e` | Customer profile and address management |
| `0edd32f` | React shopping experience |
| `12991d6` | Flutter customer experience |
| `62233cf` | Recommendation API foundation |
| `6b68f97` | Personal Stylist Agent |
| `73800f7` | Deterministic recommendation validation |
| `8dc4b85` | Shared agent workflow integration |
| `d3e7cfe` | Flutter recommendation experience |
| `1457889` | Complete component test pass |
| `8b4589e` | Security and ownership audit |
| `a7aad98` | Cross-component cart-to-checkout integration test |

### Evidence status

| Evidence type | Verifiable artifact/status |
|---|---|
| Meaningful commits | The 15 commits above are present on the feature branch |
| Feature branch | Local branch and matching remote-tracking branch both pointed to `a7aad98` at Phase 17 start |
| Pull request | No PR with this feature branch as its head was found in the public repository metadata; PR evidence is pending and must not be claimed yet |
| Tests | Commands and executed counts are recorded in the testing section; source is under `backend/SEF_Project.Api.Tests`, `frontend/SEF-Project/src/test`, and Flutter `test` |
| Screenshots | No assessment screenshots are committed; UI assets are not claimed as execution evidence |
| API documentation | Controller XML comments, generated Swagger/OpenAPI, endpoint table, and illustrative commands in this document |
| Database changes | Shopping migration, EF models/configuration, database model tests, and relational integration tests |
| Agent execution | Agent, tool, validation, workflow, malformed-output, timeout, and safe-failure tests plus persisted workflow entities; no production execution screenshot is claimed |
| React implementation | Routes, pages, components, API services, and Vitest suite in `frontend/SEF-Project` |
| Flutter implementation | Screens, secure API client, state store, models, and tests in `mobile/sef_project_mobile/sef_project` |

Recommended remaining evidence work is to open the actual pull request, attach
its URL, capture screenshots from a running integrated environment, export one
sanitized Swagger request/response, and capture a persisted workflow summary.
Those artifacts should be added only after they genuinely exist.
