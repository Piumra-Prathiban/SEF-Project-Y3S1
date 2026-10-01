import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/features/promotions/promotion_matching.dart';

import '../../support/fixtures.dart';

void main() {
  group('promotionForProduct', () {
    test('returns null when no promotion applies', () {
      final match = promotionForProduct([freeDelivery], shoppingProductWithImage);
      expect(match, isNull);
    });

    test('returns null when there are no promotions', () {
      expect(promotionForProduct(const [], shoppingProductWithImage), isNull);
    });

    test('matches a promotion targeting the product directly', () {
      final match = promotionForProduct(
        [freeDelivery, shirtFlashSale],
        shoppingProductWithImage,
      );
      expect(match?.name, 'Shirt Flash Sale');
    });

    test("matches a promotion targeting one of the product's categories", () {
      final match = promotionForProduct(
        [freeDelivery, shirtsCategorySale],
        shoppingProductWithImage,
      );
      expect(match?.name, 'Shirts Category Sale');
    });

    test('prefers the first (soonest-ending) applicable promotion', () {
      final match = promotionForProduct(
        [shirtsCategorySale, shirtFlashSale],
        shoppingProductWithImage,
      );
      expect(match?.name, 'Shirts Category Sale');
    });
  });
}
