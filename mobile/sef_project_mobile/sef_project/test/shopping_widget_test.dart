import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/app.dart';
import 'package:sef_project/models/shopping_models.dart';
import 'package:sef_project/screens/cart_screen.dart';
import 'package:sef_project/screens/products_screen.dart';
import 'package:sef_project/screens/profile_screen.dart';
import 'package:sef_project/screens/recommendations_screen.dart';
import 'package:sef_project/screens/wishlist_screen.dart';
import 'package:sef_project/services/customer_api.dart';
import 'package:sef_project/state/customer_store.dart';

import 'fake_customer_repository.dart';

Widget _screen(CustomerStore store, Widget child) => StoreScope(
  store: store,
  child: MaterialApp(home: child),
);

void main() {
  testWidgets('unauthenticated customers see the login screen', (tester) async {
    final repository = FakeCustomerRepository()..token = false;
    final store = CustomerStore(repository);
    await store.initialize();

    await tester.pumpWidget(CustomerShoppingApp(store: store));
    await tester.pumpAndSettle();

    expect(find.text('Welcome to Clothic'), findsOneWidget);
    expect(find.text('Log in'), findsOneWidget);
  });

  testWidgets('product browsing loads API results and opens details', (
    tester,
  ) async {
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

  testWidgets('product browsing exposes safe API errors and retry', (
    tester,
  ) async {
    final repository = FakeCustomerRepository()
      ..productError = const ApiException('Catalogue is unavailable.');
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const ProductsScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Catalogue is unavailable.'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
  });

  testWidgets('product filters reject malformed price text', (tester) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const ProductsScreen()));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Filters and sorting'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('product-min-price')),
      'not-a-number',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
    await tester.pump();

    expect(find.text('Enter a valid minimum price.'), findsOneWidget);
    expect(repository.lastMinPrice, isNull);
  });

  testWidgets('authenticated bottom navigation opens the stylist experience', (
    tester,
  ) async {
    final store = CustomerStore(FakeCustomerRepository());
    await store.initialize();

    await tester.pumpWidget(CustomerShoppingApp(store: store));
    await tester.pumpAndSettle();
    expect(find.text('Discover'), findsOneWidget);

    await tester.tap(find.text('Stylist'));
    await tester.pumpAndSettle();

    expect(find.text('Personal Stylist'), findsOneWidget);
    expect(find.byKey(const Key('recommendation-submit')), findsOneWidget);
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

  testWidgets('cart displays backend totals and updates quantity', (
    tester,
  ) async {
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

  testWidgets('profile displays customer and default address', (tester) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store, const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Sam Perera'), findsOneWidget);
    expect(find.text('customer@example.com'), findsOneWidget);
    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Default'), findsOneWidget);
  });

  testWidgets(
    'personal stylist displays grounded results and explicit actions',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(900, 1400));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final repository = FakeCustomerRepository()..wishlistItems = [];
      final store = CustomerStore(repository);

      await tester.pumpWidget(_screen(store, const RecommendationsScreen()));
      await tester.enterText(find.byKey(const Key('recommendation-size')), 'M');
      await tester.enterText(
        find.byKey(const Key('recommendation-colours')),
        'Blue',
      );
      final submit = find.byKey(const Key('recommendation-submit'));
      await tester.ensureVisible(submit);
      await tester.tap(submit);
      await tester.pumpAndSettle();

      expect(find.text('Workflow completed'), findsOneWidget);
      expect(find.text('Linen Shirt'), findsOneWidget);
      expect(
        find.text('Matches the requested dinner style and budget.'),
        findsOneWidget,
      );
      expect(find.text('LKR 4500.00'), findsOneWidget);
      expect(find.text('View product'), findsOneWidget);

      final addToWishlist = find.byTooltip(
        'Add recommended product to wishlist',
      );
      await tester.scrollUntilVisible(
        addToWishlist,
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(addToWishlist);
      await tester.pumpAndSettle();
      expect(repository.wishlistItems.single.productId, 'product-1');

      final addToCart = find.byTooltip('Add recommended variant to cart');
      await tester.scrollUntilVisible(
        addToCart,
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(addToCart);
      await tester.pumpAndSettle();
      expect(repository.currentCart.items.single.productVariantId, 'variant-1');
    },
  );

  testWidgets(
    'personal stylist exposes processing and validation failure states',
    (tester) async {
      final repository = FakeCustomerRepository()
        ..recommendationDelay = const Duration(seconds: 1)
        ..recommendationResult = const RecommendationResult(
          workflowId: 'workflow-failed',
          status: 'failed',
          recommendations: [],
          execution: RecommendationExecution(
            agentName: 'Personal Stylist Agent',
            status: 'failed',
            outputValidated: false,
            errorSummary: 'Validation failed.',
            validationResults: [
              RecommendationValidation(
                rule: 'Colour',
                isValid: false,
                message: 'Colour could not be verified.',
              ),
            ],
          ),
        );
      final store = CustomerStore(repository);

      await tester.pumpWidget(_screen(store, const RecommendationsScreen()));
      final submit = find.byKey(const Key('recommendation-submit'));
      await tester.ensureVisible(submit);
      await tester.tap(submit);
      await tester.pump(const Duration(milliseconds: 1));
      expect(find.text('Personal Stylist is working'), findsOneWidget);

      await tester.pump(const Duration(seconds: 1));
      await tester.pumpAndSettle();
      expect(
        find.text('Recommendations did not pass validation'),
        findsOneWidget,
      );
      expect(
        find.textContaining('Colour could not be verified'),
        findsOneWidget,
      );
      expect(find.text('Linen Shirt'), findsNothing);
    },
  );
}
