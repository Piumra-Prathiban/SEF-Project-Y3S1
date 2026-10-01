import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/order_models.dart';
import 'package:sef_project/screens/customer_orders_screen.dart';
import 'package:sef_project/state/customer_store.dart';

import '../fake_customer_repository.dart';

Order _orderWith(List<Shipment> shipments) => Order(
  id: 'order-1',
  orderNumber: 'ORD-TEST-1',
  status: 3,
  placedAt: DateTime.utc(2026, 9, 26),
  subtotal: 4500,
  discountTotal: 0,
  taxAmount: 0,
  shippingFee: 0,
  total: 4500,
  currency: 'LKR',
  items: const [
    OrderItem(
      id: 'order-item-1',
      productVariantId: 'variant-1',
      sku: 'SHIRT-BLUE-M',
      name: 'Linen Shirt',
      quantity: 1,
      unitPrice: 4500,
      lineTotal: 4500,
    ),
  ],
  payments: const [],
  shipments: shipments,
  statusHistory: const [],
);

class _ShipmentOrderRepository extends FakeCustomerRepository {
  _ShipmentOrderRepository(this.orderFixture);

  final Order orderFixture;

  @override
  Future<Order> order(String orderId) async => orderFixture;
}

Widget _detailScreen(CustomerStore store) => StoreScope(
  store: store,
  child: const MaterialApp(
    home: CustomerOrderDetailScreen(orderId: 'order-1'),
  ),
);

void main() {
  testWidgets('a shipped shipment renders progress and tracking info', (
    tester,
  ) async {
    final order = _orderWith([
      Shipment(
        id: 'shipment-1',
        status: 1,
        carrier: 'DHL',
        trackingNumber: 'TRK-123',
        shippedAt: DateTime.utc(2026, 9, 26),
      ),
    ]);
    final store = CustomerStore(_ShipmentOrderRepository(order));

    await tester.pumpWidget(_detailScreen(store));
    await tester.pumpAndSettle();

    expect(find.text('Pending'), findsOneWidget);
    expect(find.text('Shipped'), findsWidgets); // badge + progress pill
    expect(find.text('Delivered'), findsOneWidget);
    expect(find.text('Carrier: DHL'), findsOneWidget);
    expect(find.text('Tracking: TRK-123'), findsOneWidget);
    expect(find.textContaining('Shipped:'), findsOneWidget);
  });

  testWidgets('a cancelled shipment shows a terminal state without progress', (
    tester,
  ) async {
    final order = _orderWith([
      Shipment(id: 'shipment-2', status: 3, carrier: 'FedEx'),
    ]);
    final store = CustomerStore(_ShipmentOrderRepository(order));

    await tester.pumpWidget(_detailScreen(store));
    await tester.pumpAndSettle();

    expect(find.text('Cancelled'), findsOneWidget);
    expect(find.text('Carrier: FedEx'), findsOneWidget);
    expect(find.text('Pending'), findsNothing);
    expect(find.text('Shipped'), findsNothing);
    expect(find.text('Delivered'), findsNothing);
  });
}
