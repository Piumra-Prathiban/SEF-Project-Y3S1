import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/core/api/api_exception.dart';
import 'package:sef_project/features/promotions/models/promotion_models.dart';
import 'package:sef_project/features/promotions/screens/product_detail_screen.dart';
import 'package:sef_project/features/promotions/screens/promotion_detail_screen.dart';
import 'package:sef_project/features/promotions/screens/promotions_screen.dart';

import '../../support/fixtures.dart';

Widget app(Widget home) => MaterialApp(home: home);

void setScreenWidth(WidgetTester tester, double width) {
  tester.view.physicalSize = Size(width, 900);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
}

void main() {
  late FakePromotionRepository repository;

  setUp(() => repository = FakePromotionRepository());

  group('PromotionsScreen', () {
    testWidgets('shows a loading state, then the active promotions', (tester) async {
      final pending = Completer<List<Promotion>>();
      repository.onFetchActive = () => pending.future;

      await tester.pumpWidget(app(PromotionsScreen(repository: repository)));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Loading promotions…'), findsOneWidget);

      pending.complete([pizzaPromotion, freeDelivery]);
      await tester.pumpAndSettle();

      expect(find.text('Pizza 20% Off'), findsOneWidget);
      expect(find.text('20% off'), findsOneWidget);
      expect(find.text('20% off all pizzas.'), findsOneWidget);
      expect(find.text('Free delivery'), findsOneWidget);
      expect(find.text('Valid 1 Sep – 31 Dec 2026'), findsNWidgets(2));
    });

    testWidgets('shows an empty state', (tester) async {
      repository.onFetchActive = () async => [];

      await tester.pumpWidget(app(PromotionsScreen(repository: repository)));
      await tester.pumpAndSettle();

      expect(find.text('No promotions right now'), findsOneWidget);
    });

    testWidgets('shows an error with retry', (tester) async {
      var attempts = 0;
      repository.onFetchActive = () async {
        attempts++;
        if (attempts == 1) {
          throw const ApiException.network('Could not reach the server.');
        }
        return [pizzaPromotion];
      };

      await tester.pumpWidget(app(PromotionsScreen(repository: repository)));
      await tester.pumpAndSettle();

      expect(find.text('Could not reach the server.'), findsOneWidget);

      await tester.tap(find.text('Try again'));
      await tester.pumpAndSettle();

      expect(find.text('Pizza 20% Off'), findsOneWidget);
      expect(attempts, 2);
    });

    testWidgets('uses one column on phones and a grid on wide screens', (tester) async {
      setScreenWidth(tester, 400);
      await tester.pumpWidget(app(PromotionsScreen(repository: repository)));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('promotions-list')), findsOneWidget);

      setScreenWidth(tester, 1100);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('promotions-grid')), findsOneWidget);
      expect(promotionGridColumns(1100), 3);
      expect(promotionGridColumns(700), 2);
      expect(promotionGridColumns(400), 1);
    });

    testWidgets('opens the promotion details', (tester) async {
      await tester.pumpWidget(app(PromotionsScreen(repository: repository)));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Pizza 20% Off'));
      await tester.pumpAndSettle();

      expect(find.byType(PromotionDetailScreen), findsOneWidget);
      expect(find.text('Eligible products'), findsOneWidget);
    });
  });

  group('PromotionDetailScreen', () {
    testWidgets('lists eligible products with server prices and indicators', (tester) async {
      final semantics = tester.ensureSemantics();

      await tester.pumpWidget(app(PromotionDetailScreen(repository: repository, promotionId: 'promo-1')));
      await tester.pumpAndSettle();

      expect(find.text('Pizza 20% Off'), findsOneWidget);
      expect(find.text('Part of Summer Launch'), findsOneWidget);
      expect(find.text('Margherita Pizza'), findsOneWidget);
      expect(find.text('Garlic Bread'), findsOneWidget);

      // Prices come straight from the API response.
      expect(find.text('LKR 960.00'), findsOneWidget);
      final original = tester.widget<Text>(find.text('LKR 1,200.00'));
      expect(original.style?.decoration, TextDecoration.lineThrough);
      // The price label is merged into the product card's semantics node
      // (name, indicator, SKU, ...), so match within it rather than exactly.
      expect(
        find.bySemanticsLabel(RegExp(r'Now LKR 960\.00, was LKR 1,200\.00')),
        findsOneWidget,
      );
      expect(find.text('LKR 500.00'), findsOneWidget);

      // Only the discounted product carries the indicator.
      expect(find.text('On promotion'), findsOneWidget);

      semantics.dispose();
    });

    testWidgets('explains promotions that do not change product prices', (tester) async {
      await tester.pumpWidget(app(PromotionDetailScreen(repository: repository, promotionId: 'promo-2')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Applied at checkout'), findsOneWidget);
      expect(find.text('No products are currently included in this promotion.'), findsOneWidget);
    });

    testWidgets('shows the API error when the promotion is no longer live', (tester) async {
      repository.onFetchPromotion = (_) async =>
          throw ApiException.fromResponse(404, '{"status":404,"title":"Not Found"}');

      await tester.pumpWidget(app(PromotionDetailScreen(repository: repository, promotionId: 'gone')));
      await tester.pumpAndSettle();

      expect(find.text('This item is no longer available.'), findsOneWidget);
      expect(find.text('Try again'), findsOneWidget);
    });

    testWidgets('navigates to product details', (tester) async {
      await tester.pumpWidget(app(PromotionDetailScreen(repository: repository, promotionId: 'promo-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Margherita Pizza'));
      await tester.pumpAndSettle();

      expect(find.byType(ProductDetailScreen), findsOneWidget);
      expect(find.text('Active promotions'), findsOneWidget);
    });
  });

  group('ProductDetailScreen', () {
    testWidgets('shows the promotion indicator and discounted price', (tester) async {
      await tester.pumpWidget(app(ProductDetailScreen(repository: repository, productId: 'prod-1')));
      await tester.pumpAndSettle();

      expect(find.text('Margherita Pizza'), findsOneWidget);
      expect(find.text('On promotion'), findsOneWidget);
      expect(find.text('LKR 960.00'), findsOneWidget);
      expect(find.text('Pizza 20% Off applied'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Pizza 20% Off'), findsOneWidget);
    });

    testWidgets('shows the full price without an indicator when no promotion applies', (tester) async {
      await tester.pumpWidget(app(ProductDetailScreen(repository: repository, productId: 'prod-3')));
      await tester.pumpAndSettle();

      expect(find.text('Spaghetti Carbonara'), findsOneWidget);
      expect(find.text('On promotion'), findsNothing);
      expect(find.text('LKR 1,800.00'), findsOneWidget);
      expect(find.text('No active promotions for this product.'), findsOneWidget);
    });

    testWidgets('opens a promotion from the product', (tester) async {
      await tester.pumpWidget(app(ProductDetailScreen(repository: repository, productId: 'prod-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ListTile, 'Pizza 20% Off'));
      await tester.pumpAndSettle();

      expect(find.byType(PromotionDetailScreen), findsOneWidget);
    });
  });
}
