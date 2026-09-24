import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/features/promotions/models/promotion_models.dart';

import '../../support/fixtures.dart';

void main() {
  test('Promotion.fromJson reads the API promotion', () {
    final promotion = Promotion.fromJson(pizzaPromotionJson);

    expect(promotion.id, 'promo-1');
    expect(promotion.name, 'Pizza 20% Off');
    expect(promotion.type, PromotionType.percentageDiscount);
    expect(promotion.discountValue, 20);
    expect(promotion.startDate, DateTime.utc(2026, 9, 1));
    expect(promotion.endDate.isUtc, isTrue);
    expect(promotion.campaignName, 'Summer Launch');
  });

  test('PromotionType maps API numbers and tolerates unknown values', () {
    expect(PromotionType.fromJson(1), PromotionType.fixedAmountDiscount);
    expect(PromotionType.fromJson(3), PromotionType.freeShipping);
    expect(PromotionType.fromJson(42), PromotionType.unknown);
    expect(PromotionType.fromJson(null), PromotionType.unknown);
  });

  test('VariantOffer keeps the server prices', () {
    final offer = VariantOffer.fromJson(margheritaSmallOfferJson);

    expect(offer.originalPrice, 1200);
    expect(offer.discountAmount, 240);
    expect(offer.finalPrice, 960);
    expect(offer.isDiscounted, isTrue);
    expect(VariantOffer.fromJson(garlicBreadOfferJson).isDiscounted, isFalse);
  });

  test('PromotionProducts and ProductOffer parse nested products', () {
    final offers = PromotionProducts.fromJson(pizzaPromotionProductsJson);

    expect(offers.hasPriceDiscount, isTrue);
    expect(offers.products, hasLength(2));
    expect(offers.products[0].hasDiscount, isTrue);
    expect(offers.products[1].hasDiscount, isFalse);
  });

  test('ProductPromotions parses promotions and variants', () {
    final product = ProductPromotions.fromJson(margheritaPromotionsJson);

    expect(product.hasActivePromotion, isTrue);
    expect(product.promotions.single.name, 'Pizza 20% Off');
    expect(product.variants.single.finalPrice, 960);
  });
}
