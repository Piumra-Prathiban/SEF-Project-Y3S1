import 'package:flutter/material.dart';

import 'auth/auth_controller.dart';
import 'auth/auth_scope.dart';
import 'screens/login_screen.dart';
import 'screens/orders_screen.dart';
import 'services/api_client.dart';
import 'services/auth_service.dart';
import 'services/order_service.dart';

void main() {
  final apiClient = ApiClient();

  runApp(
    SefApp(
      authController: AuthController(AuthService(apiClient)),
      orderService: OrderService(apiClient),
    ),
  );
}

class SefApp extends StatelessWidget {
  const SefApp({
    super.key,
    required this.authController,
    required this.orderService,
  });

  final AuthController authController;
  final OrderService orderService;

  @override
  Widget build(BuildContext context) {
    return AuthScope(
      controller: authController,
      child: MaterialApp(
        title: 'SEF Fashion',
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFFAA3BFF)),
          useMaterial3: true,
        ),
        home: AuthGate(orderService: orderService),
      ),
    );
  }
}

/// Shows the sign-in screen while there is no session, otherwise the customer's
/// orders. A 401 from the API calls the same logout path, so an expired or
/// invalid session lands back here - matching the web app's behaviour.
class AuthGate extends StatelessWidget {
  const AuthGate({super.key, required this.orderService});

  final OrderService orderService;

  @override
  Widget build(BuildContext context) {
    final auth = AuthScope.of(context);

    if (!auth.isAuthenticated) {
      return const LoginScreen();
    }

    return OrdersScreen(
      orderService: orderService,
      token: auth.token!,
      onSignOut: auth.logout,
    );
  }
}
