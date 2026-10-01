import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/screens/products_screen.dart';
import 'package:sef_project/state/customer_store.dart';

import '../fake_customer_repository.dart';
import '../support/fixtures.dart';

Widget _screen(Widget child) => StoreScope(
  store: CustomerStore(FakeCustomerRepository()),
  child: MaterialApp(home: child),
);

void main() {
  testWidgets('ProductTile shows an offer badge when a promotion applies', (
    tester,
  ) async {
    await tester.pumpWidget(
      _screen(
        ProductTile(
          product: shoppingProductWithImage,
          promotions: [shirtFlashSale],
        ),
      ),
    );

    expect(find.text('15% off'), findsOneWidget);
  });

  testWidgets('ProductTile shows no offer badge when nothing applies', (
    tester,
  ) async {
    await tester.pumpWidget(
      _screen(
        ProductTile(
          product: shoppingProductWithImage,
          promotions: [freeDelivery],
        ),
      ),
    );

    expect(find.textContaining('% off'), findsNothing);
  });

  testWidgets('ProductTile shows no offer badge when no promotions were fetched', (
    tester,
  ) async {
    await tester.pumpWidget(
      _screen(ProductTile(product: shoppingProductWithImage)),
    );

    expect(find.textContaining('% off'), findsNothing);
  });

  testWidgets(
    'ProductsScreen fetches active promotions once and badges matching cards',
    (tester) async {
      final promotionRepository = FakePromotionRepository()
        ..onFetchActive = () async => [shirtFlashSale];

      await tester.pumpWidget(
        StoreScope(
          store: CustomerStore(FakeCustomerRepository()),
          child: MaterialApp(
            home: ProductsScreen(promotionRepository: promotionRepository),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(promotionRepository.activeCalls, 1);
      expect(find.text('15% off'), findsOneWidget);
    },
  );
}
