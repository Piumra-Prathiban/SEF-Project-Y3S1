import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/app.dart';
import 'package:sef_project/screens/cart_screen.dart';
import 'package:sef_project/screens/products_screen.dart';
import 'package:sef_project/screens/profile_screen.dart';
import 'package:sef_project/screens/wishlist_screen.dart';
import 'package:sef_project/state/customer_store.dart';

import 'fake_customer_repository.dart';

Widget _screen(CustomerStore store, Widget child) => StoreScope(
      store: store,
      child: MaterialApp(home: child),
    );

void main() {
  testWidgets('unauthenticated customers see the login screen',
      (tester) async {
    final repository = FakeCustomerRepository()..token = false;
    final store = CustomerStore(repository);
    await store.initialize();

    await tester.pumpWidget(CustomerShoppingApp(store: store));
    await tester.pumpAndSettle();

    expect(find.text('Welcome to Mode'), findsOneWidget);
    expect(find.text('Log in'), findsOneWidget);
  });

  testWidgets('product browsing loads API results and opens details',
      (tester) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const ProductsScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Linen Shirt'), findsOneWidget);
    expect(find.text('In stock'), findsOneWidget);

    await tester.tap(find.text('Linen Shirt'));
    await tester.pumpAndSettle();

    expect(find.text('Available variants'), findsOneWidget);
    expect(find.textContaining('Blue / Medium'), findsOneWidget);
    expect(find.text('Add to cart'), findsOneWidget);
  });

  testWidgets('wishlist removes a saved product', (tester) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const WishlistScreen()));
    await tester.pumpAndSettle();
    expect(find.text('Linen Shirt'), findsOneWidget);

    await tester.tap(find.byTooltip('Remove from wishlist'));
    await tester.pumpAndSettle();

    expect(find.text('Your wishlist is empty'), findsOneWidget);
  });

  testWidgets('cart displays backend totals and updates quantity',
      (tester) async {
    final repository = FakeCustomerRepository();
    repository.currentCart = await repository.addCart('variant-1', 1);
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const CartScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Total'), findsOneWidget);
    expect(find.text('LKR 4500.00'), findsWidgets);

    await tester.tap(find.byTooltip('Increase quantity'));
    await tester.pumpAndSettle();

    expect(find.text('2'), findsOneWidget);
    expect(find.text('LKR 9000.00'), findsWidgets);
  });

  testWidgets('profile displays customer and default address',
      (tester) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Sam Perera'), findsOneWidget);
    expect(find.text('customer@example.com'), findsOneWidget);
    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Default'), findsOneWidget);
  });
}
