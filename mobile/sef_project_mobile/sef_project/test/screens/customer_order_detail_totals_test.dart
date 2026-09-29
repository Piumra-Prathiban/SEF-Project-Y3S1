import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/order_models.dart';
import 'package:sef_project/screens/customer_orders_screen.dart';
import 'package:sef_project/state/customer_store.dart';

import '../fake_customer_repository.dart';

Order _orderWith({required double discountTotal, String? couponCode}) => Order(
  id: 'order-1',
  orderNumber: 'ORD-TEST-1',
  status: 0,
  placedAt: DateTime.utc(2026, 9, 26),
  subtotal: 5000,
  discountTotal: discountTotal,
  taxAmount: 0,
  shippingFee: 0,
  total: 5000 - discountTotal,
  currency: 'LKR',
  // A line total distinct from both the subtotal and the net total, so
  // asserting on the totals section below can't collide with an item row.
  items: const [
    OrderItem(
      id: 'order-item-1',
      productVariantId: 'variant-1',
      sku: 'TSH-CLS-XS',
      name: 'Classic Cotton T-Shirt',
      quantity: 2,
      unitPrice: 1900,
      lineTotal: 3800,
    ),
  ],
  payments: const [],
  shipments: const [],
  statusHistory: const [],
  couponCode: couponCode,
);

class _FixedOrderRepository extends FakeCustomerRepository {
  _FixedOrderRepository(this.orderFixture);

  final Order orderFixture;

  @override
  Future<Order> order(String orderId) async => orderFixture;
}

Widget _detailScreen(CustomerStore store) => StoreScope(
  store: store,
  child: const MaterialApp(home: CustomerOrderDetailScreen(orderId: 'order-1')),
);

void main() {
  testWidgets(
    'order detail shows the gross, discount, net totals and applied coupon',
    (tester) async {
      final order = _orderWith(discountTotal: 1000, couponCode: 'SUMMER20');
      final store = CustomerStore(_FixedOrderRepository(order));

      await tester.pumpWidget(_detailScreen(store));
      await tester.pumpAndSettle();

      expect(find.text('LKR 5,000.00'), findsOneWidget); // subtotal
      expect(find.text('- LKR 1,000.00'), findsOneWidget); // discount
      expect(find.text('LKR 4,000.00'), findsOneWidget); // net total
      expect(find.text('Coupon SUMMER20 applied'), findsOneWidget);
    },
  );

  testWidgets(
    'order detail hides the discount and coupon rows when none applied',
    (tester) async {
      final order = _orderWith(discountTotal: 0);
      final store = CustomerStore(_FixedOrderRepository(order));

      await tester.pumpWidget(_detailScreen(store));
      await tester.pumpAndSettle();

      expect(find.text('Discount'), findsNothing);
      expect(find.textContaining('Coupon'), findsNothing);
      // Subtotal and total both read LKR 5,000.00 with no discount applied.
      expect(find.text('LKR 5,000.00'), findsNWidgets(2));
    },
  );
}
