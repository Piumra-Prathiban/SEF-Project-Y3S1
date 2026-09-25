# Member 1 Frontend Integration Contract

This document describes the Product & Inventory Management backend contract for
React and Flutter consumers. Do not call the database or AI layer directly from
frontend clients; all access must go through these ASP.NET Core REST endpoints.

## Common API rules

- Base path: `/api`
- Authentication: JWT bearer token in `Authorization: Bearer <token>`.
- Read endpoints require an authenticated user.
- Write endpoints require `Staff` or `Administrator`.
- A customer role must not create/update/delete catalog records or adjust stock.
- IDs are GUIDs unless the field is `PerformedByUserId`, which is an integer.
- Date/time fields are returned as JSON `DateTime` values from the backend.
- Validation failures return `400 application/problem+json` with
  `ValidationProblemDetails`.
- Not found failures return `404 application/problem+json`.
- Duplicate/business conflicts return `409 application/problem+json`.
- Missing/invalid token returns `401 application/problem+json`.
- Valid token without required role returns `403 application/problem+json`.

## Pagination contract

`GET /api/products` returns a paginated response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Supported query parameters:

- `search`
- `categoryId`
- `collectionId`
- `isActive`
- `minPrice`
- `maxPrice`
- `sortBy`: `name`, `price`, `created`, `createdAt`, `createdDate`
- `sortDirection`: `asc` or `desc`
- `page`
- `pageSize`

`page` is normalized to at least `1`. `pageSize` is clamped between `1` and
`100`.

## Products

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/products` | Authenticated | Query parameters | `PagedResponse<ProductResponseDto>` |
| GET | `/api/products/{id}` | Authenticated | None | `ProductResponseDto` |
| GET | `/api/products/{id}/variants` | Authenticated | None | `ProductVariantResponseDto[]` |
| POST | `/api/products` | Staff/Admin | `ProductCreateDto` | `201 ProductResponseDto` |
| PUT | `/api/products/{id}` | Staff/Admin | `ProductUpdateDto` | `ProductResponseDto` |
| DELETE | `/api/products/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- `ProductResponseDto` includes `categoryId`, `categoryName`, `collectionId`,
  `collectionName`, optional supplier fields, `isActive`, timestamps and nested
  `variants`.
- Deletes are soft deletes through `isActive = false`.
- Product create/update requires valid `categoryId` and `collectionId`.

## Categories

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/categories` | Authenticated | None | `CategoryResponseDto[]` |
| GET | `/api/categories/{id}` | Authenticated | None | `CategoryResponseDto` |
| POST | `/api/categories` | Staff/Admin | `CategoryCreateDto` | `201 CategoryResponseDto` |
| PUT | `/api/categories/{id}` | Staff/Admin | `CategoryUpdateDto` | `CategoryResponseDto` |
| DELETE | `/api/categories/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- Category names must be unique.
- Deletes are soft deletes through `isActive = false`.

## Collections

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/collections` | Authenticated | None | `CollectionResponseDto[]` |
| GET | `/api/collections/{id}` | Authenticated | None | `CollectionResponseDto` |
| POST | `/api/collections` | Staff/Admin | `CollectionCreateDto` | `201 CollectionResponseDto` |
| PUT | `/api/collections/{id}` | Staff/Admin | `CollectionUpdateDto` | `CollectionResponseDto` |
| DELETE | `/api/collections/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- Collection names must be unique.
- Deletes are soft deletes through `isActive = false`.

## Sizes

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/sizes` | Authenticated | None | `SizeResponseDto[]` |
| GET | `/api/sizes/{id}` | Authenticated | None | `SizeResponseDto` |
| POST | `/api/sizes` | Staff/Admin | `SizeCreateDto` | `201 SizeResponseDto` |
| PUT | `/api/sizes/{id}` | Staff/Admin | `SizeUpdateDto` | `SizeResponseDto` |
| DELETE | `/api/sizes/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- Sizes are returned ordered by `displayOrder`, then name.
- `displayOrder` must be non-negative.

## Colours

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/colours` | Authenticated | None | `ColourResponseDto[]` |
| GET | `/api/colours/{id}` | Authenticated | None | `ColourResponseDto` |
| POST | `/api/colours` | Staff/Admin | `ColourCreateDto` | `201 ColourResponseDto` |
| PUT | `/api/colours/{id}` | Staff/Admin | `ColourUpdateDto` | `ColourResponseDto` |
| DELETE | `/api/colours/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- `hexCode` is optional, but when provided must match `#RRGGBB`.

## Product variants

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/variants/{id}` | Authenticated | None | `ProductVariantResponseDto` |
| POST | `/api/products/{productId}/variants` | Staff/Admin | `ProductVariantCreateDto` | `201 ProductVariantResponseDto` |
| PUT | `/api/variants/{id}` | Staff/Admin | `ProductVariantUpdateDto` | `ProductVariantResponseDto` |
| DELETE | `/api/variants/{id}` | Staff/Admin | None | `204 No Content` |

Frontend notes:

- Use `/api/products/{productId}/variants` to list/create variants for a product.
- The route `productId` is authoritative during create; any body `productId` is
  overwritten by the controller.
- `sku` must be unique.
- Product + size + colour must be unique.
- Price and stock-related quantities must be non-negative.
- `ProductVariantResponseDto` includes product, size, colour names and embedded
  `InventoryResponseDto` when inventory exists.

## Inventory

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/inventory` | Authenticated | None | `InventoryResponseDto[]` |
| GET | `/api/inventory/low-stock` | Authenticated | None | `InventoryResponseDto[]` |
| GET | `/api/inventory/{variantId}` | Authenticated | None | `InventoryResponseDto` |
| GET | `/api/inventory/{variantId}/history` | Authenticated | None | `StockTransactionResponseDto[]` |
| POST | `/api/inventory/{variantId}/adjust` | Staff/Admin | `StockAdjustmentDto` | `InventoryResponseDto` |

Frontend notes:

- Low stock means `quantityOnHand <= reorderLevel`.
- Available stock is `quantityOnHand - reservedQuantity`.
- Supported manual adjustment types are `StockIn`, `StockOut`, and `Adjustment`.
- Stock updates are transactional and always create stock history.
- Stock cannot become negative or fall below reserved quantity.
- The route `variantId` is authoritative during adjustment; any body
  `productVariantId` is overwritten by the controller.

## Inventory analysis agent

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| POST | `/api/agents/inventory-analysis` | Staff/Admin | `InventoryAnalysisRequestDto` | `InventoryAnalysisResponseDto` |

Frontend notes:

- This endpoint runs analysis only. It does not directly change stock.
- Use the workflow endpoints below when human approval and execution are needed.

## Inventory agent workflows

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| POST | `/api/inventory/agent/workflows` | Staff/Admin | `InventoryAnalysisRequestDto` | `201 InventoryAgentWorkflowResponseDto` |
| GET | `/api/inventory/agent/workflows/{id}` | Staff/Admin | None | `InventoryAgentWorkflowResponseDto` |
| POST | `/api/inventory/agent/workflows/{id}/approve` | Staff/Admin | `InventoryAgentApprovalRequestDto` | `InventoryAgentWorkflowResponseDto` |
| POST | `/api/inventory/agent/workflows/{id}/reject` | Staff/Admin | `InventoryAgentApprovalRequestDto` | `InventoryAgentWorkflowResponseDto` |
| POST | `/api/inventory/agent/workflows/{id}/revise` | Staff/Admin | `InventoryAgentRevisionRequestDto` | `InventoryAgentWorkflowResponseDto` |

Frontend notes:

- Workflow approval is required before agent recommendations can trigger a stock
  operation.
- Approval executes through the normal inventory service, so all deterministic
  business rules still apply.
- The system persists workflow state, visible steps, validation summaries,
  approval status, final outcome, errors and timestamps.
- Hidden model reasoning is not persisted or returned.

## Swagger

Swagger is available in development at:

- `/swagger`

Use the `Bearer` security scheme in Swagger UI with a valid JWT token.
