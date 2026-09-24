import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/screens/orders_screen.dart';
import 'package:sef_project/services/order_service.dart';

import '../fixtures.dart';

Widget wrap(OrderService service, {VoidCallback? onSignOut}) => MaterialApp(
      home: OrdersScreen(
        orderService: service,
        token: testToken,
        onSignOut: onSignOut ?? () {},
      ),
    );

void main() {
  group('OrdersScreen', () {
    testWidgets('shows a loading indicator then the customer orders',
        (tester) async {
      final service = orderServiceWith((_) async => okJson(orderListJson()));

      await tester.pumpWidget(wrap(service));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      await tester.pumpAndSettle();

      expect(find.text('ORD-1001'), findsOneWidget);
      expect(find.text('ORD-1002'), findsOneWidget);
      expect(find.text('Pending'), findsOneWidget);
      expect(find.text('Completed'), findsOneWidget);
      expect(find.text('LKR 2,505.50'), findsOneWidget);
      expect(find.textContaining('Sep 2026'), findsWidgets);
      expect(find.textContaining('Load more'), findsNothing);
    });

    testWidgets('shows the empty state when there are no orders',
        (tester) async {
      final service = orderServiceWith(
        (_) async => okJson(orderListJson(items: [], totalCount: 0)),
      );

      await tester.pumpWidget(wrap(service));
      await tester.pumpAndSettle();

      expect(find.text('You have no orders yet.'), findsOneWidget);
    });

    testWidgets('shows the backend error and recovers via retry',
        (tester) async {
      var calls = 0;
      final service = orderServiceWith((_) async {
        calls++;

        return calls == 1
            ? errorJson(500, 'Server exploded')
            : okJson(orderListJson());
      });

      await tester.pumpWidget(wrap(service));
      await tester.pumpAndSettle();

      expect(find.text('Server exploded'), findsOneWidget);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('ORD-1001'), findsOneWidget);
    });

    testWidgets('appends the next page when Load more is tapped',
        (tester) async {
      final service = orderServiceWith((request) async {
        if (request.url.queryParameters['page'] == '2') {
          return okJson(
            orderListJson(
              items: [
                orderSummaryJson(
                  id: '33333333-3333-3333-3333-333333333333',
                  orderNumber: 'ORD-2001',
                  status: 1,
                ),
              ],
              totalCount: 3,
              page: 2,
            ),
          );
        }

        return okJson(orderListJson(totalCount: 3));
      });

      await tester.pumpWidget(wrap(service));
      await tester.pumpAndSettle();

      expect(find.text('ORD-2001'), findsNothing);

      await tester.tap(find.textContaining('Load more'));
      await tester.pumpAndSettle();

      expect(find.text('ORD-2001'), findsOneWidget);
      expect(find.text('ORD-1001'), findsOneWidget);
      expect(find.textContaining('Load more'), findsNothing);
    });

    testWidgets('signs the customer out when the session expires',
        (tester) async {
      var signedOut = false;
      final service = orderServiceWith(
        (_) async => errorJson(401, 'Unauthorized'),
      );

      await tester.pumpWidget(
        wrap(service, onSignOut: () => signedOut = true),
      );
      await tester.pumpAndSettle();

      expect(signedOut, isTrue);
    });

    testWidgets('opens the order details for a tapped order', (tester) async {
      final service = orderServiceWith((request) async {
        if (request.url.path == '/api/Orders/$testOrderId') {
          return okJson(orderDetailJson());
        }

        return okJson(orderListJson());
      });

      await tester.pumpWidget(wrap(service));
      await tester.pumpAndSettle();

      await tester.tap(find.text('ORD-1001'));
      await tester.pumpAndSettle();

      expect(find.text('Slim Fit Denim Jacket'), findsOneWidget);
    });
  });
}
