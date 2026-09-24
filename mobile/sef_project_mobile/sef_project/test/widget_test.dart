import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/auth/auth_controller.dart';
import 'package:sef_project/main.dart';
import 'package:sef_project/services/auth_service.dart';
import 'package:sef_project/services/order_service.dart';

import 'fixtures.dart';

void main() {
  testWidgets('signs in and shows the customer orders', (tester) async {
    final apiClient = apiClientWith((request) async {
      if (request.url.path.endsWith('/Auth/login')) {
        return okJson(loginJson());
      }

      if (request.url.path.endsWith('/Orders')) {
        return okJson(orderListJson());
      }

      return errorJson(404, 'Not found');
    });

    await tester.pumpWidget(
      SefApp(
        authController: AuthController(AuthService(apiClient)),
        orderService: OrderService(apiClient),
      ),
    );

    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(find.text('My Orders'), findsNothing);

    await tester.enterText(
      find.byKey(const Key('login-email')),
      'customer@example.com',
    );
    await tester.enterText(
      find.byKey(const Key('login-password')),
      'secret123',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('My Orders'), findsOneWidget);
    expect(find.text('ORD-1001'), findsOneWidget);
  });

  testWidgets('shows the sign-in error for invalid credentials',
      (tester) async {
    final apiClient = apiClientWith(
      (_) async => errorJson(401, 'Invalid email or password.'),
    );

    await tester.pumpWidget(
      SefApp(
        authController: AuthController(AuthService(apiClient)),
        orderService: OrderService(apiClient),
      ),
    );

    await tester.enterText(
      find.byKey(const Key('login-email')),
      'customer@example.com',
    );
    await tester.enterText(
      find.byKey(const Key('login-password')),
      'wrong-password',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Invalid email or password.'), findsOneWidget);
    expect(find.text('My Orders'), findsNothing);
  });
}
