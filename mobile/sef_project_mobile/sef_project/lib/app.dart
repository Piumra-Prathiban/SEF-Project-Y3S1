import 'package:flutter/material.dart';
import 'screens/home_shell.dart';
import 'screens/login_screen.dart';
import 'state/customer_store.dart';

class CustomerShoppingApp extends StatelessWidget {
  const CustomerShoppingApp({super.key, required this.store});
  final CustomerStore store;
  @override
  Widget build(BuildContext context) => StoreScope(
    store: store,
    child: MaterialApp(
      title: 'Mode',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xff2e5944)),
        scaffoldBackgroundColor: const Color(0xfff7f4ee),
        cardTheme: const CardThemeData(margin: EdgeInsets.zero, elevation: 0, color: Color(0xfffffdfa)),
        inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder(), filled: true, fillColor: Colors.white),
      ),
      home: const _AuthGate(),
    ),
  );
}

class _AuthGate extends StatelessWidget {
  const _AuthGate();
  @override Widget build(BuildContext context) => StoreScope.of(context).authenticated ? const HomeShell() : const LoginScreen();
}
