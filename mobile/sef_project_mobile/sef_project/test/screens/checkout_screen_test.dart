import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/order_models.dart';
import 'package:sef_project/screens/cart_screen.dart';
import 'package:sef_project/services/customer_api.dart';
import 'package:sef_project/state/customer_store.dart';

import '../fake_customer_repository.dart';

Widget _screen(CustomerStore store) =>
    StoreScope(store: store, child: const MaterialApp(home: CartScreen()));

class _CouponOrderRepository extends FakeCustomerRepository {
  _CouponOrderRepository({this.rejectWith});

  /// When set, createOrder throws this instead of returning an order —
  /// simulates the backend rejecting an invalid/expired/over-redeemed code.
  final ApiException? rejectWith;

  @override
  Future<Order> createOrder({
    required List<Map<String, dynamic>> items,
    required Map<String, dynamic> deliveryAddress,
    required int paymentMethod,
    String? couponCode,
  }) async {
    if (rejectWith != null) {
      throw rejectWith!;
    }

    return Order(
      id: 'order-1',
      orderNumber: 'ORD-TEST-1',
      status: 0,
      placedAt: DateTime.utc(2026, 9, 26),
      subtotal: 5000,
      discountTotal: 1000,
      taxAmount: 0,
      shippingFee: 0,
      total: 4000,
      currency: 'LKR',
      items: const [],
      payments: const [],
      shipments: const [],
      statusHistory: const [],
      couponCode: couponCode,
    );
  }
}

Future<void> _goToCheckoutAndSubmit(
  WidgetTester tester, {
  String? couponCode,
}) async {
  await tester.tap(find.text('Continue to checkout'));
  await tester.pumpAndSettle();

  if (couponCode != null) {
    // The coupon field sits below the fold; scroll it into the sliver's
    // build range before entering text (ListView only mounts children near
    // the viewport).
    await tester.drag(find.byType(ListView).last, const Offset(0, -300));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('checkout-coupon-code')),
      couponCode,
    );
  }

  await tester.drag(find.byType(ListView).last, const Offset(0, -900));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Place order'));
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 100));
}

void main() {
  testWidgets(
    'a successful order with a coupon shows the savings in the confirmation',
    (tester) async {
      final repository = _CouponOrderRepository();
      repository.currentCart = await repository.addCart('variant-1', 1);
      final store = CustomerStore(repository);

      await tester.pumpWidget(_screen(store));
      await tester.pumpAndSettle();

      await _goToCheckoutAndSubmit(tester, couponCode: 'summer20');

      expect(find.text('Order placed'), findsOneWidget);
      expect(
        find.textContaining('Coupon SUMMER20 saved you'),
        findsOneWidget,
      );
    },
  );

  testWidgets(
    'a rejected coupon shows the backend error instead of placing the order',
    (tester) async {
      final repository = _CouponOrderRepository(
        rejectWith: const ApiException(
          'Coupon code has reached its usage limit.',
          statusCode: 409,
        ),
      );
      repository.currentCart = await repository.addCart('variant-1', 1);
      final store = CustomerStore(repository);

      await tester.pumpWidget(_screen(store));
      await tester.pumpAndSettle();

      await _goToCheckoutAndSubmit(tester, couponCode: 'summer20');

      expect(find.text('Order placed'), findsNothing);
      expect(
        find.text('Coupon code has reached its usage limit.'),
        findsOneWidget,
      );
    },
  );
}
