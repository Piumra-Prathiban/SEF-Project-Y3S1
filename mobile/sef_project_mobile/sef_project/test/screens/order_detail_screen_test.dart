import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/screens/order_detail_screen.dart';
import 'package:sef_project/services/order_service.dart';

import '../fixtures.dart';

// The detail screen is a long list; a tall surface keeps every section built so
// the assertions can see the later ones (timeline, shipments).
Future<void> pumpDetail(
  WidgetTester tester,
  OrderService service, {
  VoidCallback? onSignOut,
}) async {
  tester.view.physicalSize = const Size(1000, 3000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    MaterialApp(
      home: OrderDetailScreen(
        orderService: service,
        token: testToken,
        orderId: testOrderId,
        onSignOut: onSignOut ?? () {},
      ),
    ),
  );
}

void main() {
  group('OrderDetailScreen', () {
    testWidgets(
      'renders the order, items, totals, address, payment, shipment and timeline',
      (tester) async {
        final service = orderServiceWith((_) async => okJson(orderDetailJson()));

        await pumpDetail(tester, service);

        expect(find.byType(CircularProgressIndicator), findsOneWidget);

        await tester.pumpAndSettle();

        expect(find.text('ORD-1001'), findsWidgets);
        expect(find.text('Slim Fit Denim Jacket'), findsOneWidget);
        expect(find.text('DEN-001 · Qty 2'), findsOneWidget);
        expect(find.textContaining('LKR 1,150.00 each'), findsOneWidget);

        expect(find.text('Subtotal'), findsOneWidget);
        expect(find.text('Discount'), findsOneWidget);
        expect(find.text('Tax'), findsOneWidget);
        expect(find.text('Shipping'), findsOneWidget);
        expect(find.text('LKR 2,505.50'), findsOneWidget);

        expect(find.text('Asha Perera'), findsOneWidget);
        expect(find.text('12 Galle Road'), findsOneWidget);
        expect(find.text('00300 Sri Lanka'), findsOneWidget);

        expect(find.text('Card · LKR 2,505.50'), findsOneWidget);
        expect(find.text('Reference MOCK-1234'), findsOneWidget);
        expect(find.text('LankaExpress · TRACK-99'), findsOneWidget);

        expect(find.text('Status history'), findsOneWidget);
        expect(find.text('Order placed'), findsOneWidget);
        expect(find.text('Completed'), findsWidgets);
      },
    );

    testWidgets('shows empty section states when there is nothing to display',
        (tester) async {
      final service = orderServiceWith(
        (_) async => okJson(
          orderDetailJson(payments: [], shipments: [], statusHistory: []),
        ),
      );

      await pumpDetail(tester, service);
      await tester.pumpAndSettle();

      expect(find.text('No payments recorded.'), findsOneWidget);
      expect(find.text('No shipments recorded.'), findsOneWidget);
      expect(find.text('No status changes recorded.'), findsOneWidget);
    });

    testWidgets('shows a not-found state when the order is not visible',
        (tester) async {
      final service = orderServiceWith(
        (_) async => errorJson(404, 'Order not found.'),
      );

      await pumpDetail(tester, service);
      await tester.pumpAndSettle();

      expect(find.text('Order not found'), findsOneWidget);
    });

    testWidgets('surfaces a permission error (403)', (tester) async {
      final service = orderServiceWith(
        (_) async => errorJson(403, 'You cannot view this order.'),
      );

      await pumpDetail(tester, service);
      await tester.pumpAndSettle();

      expect(find.text('You cannot view this order.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('shows the backend error and recovers via retry',
        (tester) async {
      var calls = 0;
      final service = orderServiceWith((_) async {
        calls++;

        return calls == 1
            ? errorJson(409, 'Order is locked')
            : okJson(orderDetailJson());
      });

      await pumpDetail(tester, service);
      await tester.pumpAndSettle();

      expect(find.text('Order is locked'), findsOneWidget);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('Slim Fit Denim Jacket'), findsOneWidget);
    });

    testWidgets('signs the customer out when the session expires',
        (tester) async {
      var signedOut = false;
      final service = orderServiceWith(
        (_) async => errorJson(401, 'Unauthorized'),
      );

      await pumpDetail(tester, service, onSignOut: () => signedOut = true);
      await tester.pumpAndSettle();

      expect(signedOut, isTrue);
    });
  });
}
