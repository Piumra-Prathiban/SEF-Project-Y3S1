# Member 3 — Orders & Fulfilment

Member 3 owns checkout, payments, order retrieval and status, cancellation,
returns, and shipment fulfilment. The implementation is shared by the React
staff/customer web app and the Flutter customer app through the ASP.NET Core
API.

## Customer flow

1. Checkout submits only variant IDs, quantities, a delivery-address snapshot,
   and a payment method.
2. The API reloads authoritative prices and inventory, creates the order and a
   pending payment, and reserves inventory in one transaction.
3. Customers can view their own orders, payments, tracking, and history.
4. A pre-fulfilment cancellation releases reserved stock, cancels pending
   shipments, fails pending payments, and refunds completed payments.
5. After completion, the customer can request quantities from the original
   order items. Duplicate active requests cannot exceed the purchased quantity.

## Staff flow

- Order: `Pending → Confirmed → Preparing → Ready → Completed`.
- Payment: `Pending → Completed | Failed`; completed payments may be refunded
  by the controlled payment or return workflows.
- Shipment: `Pending → Shipped → Delivered`; delivery completes a ready order.
- Return: `Requested → Approved → Received → Refunded`, or
  `Requested → Rejected`. A customer may cancel only while requested.

Directly changing a completed order to refunded is disabled. Refund completion
belongs to the return workflow so returned quantities, stock, payments, and
audit history cannot diverge.

## Return integrity

- `Returns` and `ReturnItems` are durable PostgreSQL tables created by the
  `AddReturnsWorkflow` migration.
- `ReturnItem.UnitRefundAmount` snapshots the original order-item unit price.
- Requested, approved, received, and refunded quantities count against the
  remaining returnable quantity.
- Confirming receipt restores `InventoryStock.QuantityOnHand` and appends an
  `InventoryTransactionType.Return` ledger entry.
- Issuing the refund checks the completed-payment balance and creates a
  `Refunded` payment referencing the return number.
- Once every purchased item is refunded, the order becomes `Refunded` and an
  `OrderStatusHistory` record is appended.
- Customers receive not-found responses for another customer's returns; staff
  and administrators can manage all returns.

## API surface

Orders retain the existing `api/orders` checkout, list, detail, history,
payment, shipment, status, and cancellation endpoints. Returns add:

- `GET api/returns`
- `GET api/returns/{id}`
- `GET api/orders/{orderId}/returns`
- `POST api/orders/{orderId}/returns` — Customer
- `PATCH api/returns/{id}/status` — Staff/Administrator
- `POST api/returns/{id}/cancel`

## User interfaces

React routes:

- `/checkout`
- `/orders`
- `/orders/:id`
- `/returns`

The order detail page contains payment, shipment, cancellation, and return
actions. The returns list provides status and order-number filtering.

Flutter now exposes an Orders destination in the active shopping shell. Its
cart opens a real checkout form with saved-profile defaults, creates the order,
and clears the server cart only after success. Order detail includes items,
totals, payment and fulfilment state, cancellation, return creation, and return
cancellation.

## Verification

- Backend: 629 tests passed.
- React: 355 tests passed; ESLint and the Vite production build passed.
- Flutter: 85 tests passed; static analysis passed.
- EF Core reports no model changes missing from migrations.
- The migration was applied to the configured PostgreSQL database and the live
  authenticated returns list returned HTTP 200.
