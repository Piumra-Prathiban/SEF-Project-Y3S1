import 'package:sef_project/features/promotions/data/promotion_repository.dart';
import 'package:sef_project/features/promotions/models/promotion_models.dart';

// JSON shaped exactly like the ASP.NET Core Marketing API responses.

const Map<String, dynamic> topsPromotionJson = {
  'id': 'promo-1',
  'campaignId': 'camp-1',
  'campaignName': 'Summer Launch',
  'name': 'Tops 20% Off',
  'description': '20% off all tops.',
  'type': 0,
  'discountValue': 20.0,
  'startDate': '2026-09-01T00:00:00Z',
  'endDate': '2026-12-31T00:00:00Z',
  'isActive': true,
  'productIds': ['prod-1'],
  'categoryIds': <String>[],
  'createdAt': '2026-09-01T00:00:00Z',
  'updatedAt': '2026-09-01T00:00:00Z',
};

const Map<String, dynamic> freeDeliveryJson = {
  'id': 'promo-2',
  'campaignId': null,
  'campaignName': null,
  'name': 'Free Delivery',
  'description': 'Free delivery with a coupon code.',
  'type': 3,
  'discountValue': 0,
  'startDate': '2026-09-01T00:00:00Z',
  'endDate': '2026-12-31T00:00:00Z',
  'isActive': true,
  'productIds': <String>[],
  'categoryIds': <String>[],
  'createdAt': '2026-09-01T00:00:00Z',
  'updatedAt': '2026-09-01T00:00:00Z',
};

const Map<String, dynamic> tShirtXsOfferJson = {
  'productVariantId': 'var-1',
  'sku': 'TSH-CLS-XS',
  'name': 'XS / Black',
  'originalPrice': 2500.0,
  'discountAmount': 500.0,
  'finalPrice': 2000.0,
  'promotionId': 'promo-1',
  'promotionName': 'Tops 20% Off',
  'currency': 'LKR',
};

const Map<String, dynamic> hoodieMOfferJson = {
  'productVariantId': 'var-2',
  'sku': 'HOD-FLC-M',
  'name': 'M / Navy',
  'originalPrice': 6500.0,
  'discountAmount': 0.0,
  'finalPrice': 6500.0,
  'promotionId': null,
  'promotionName': null,
  'currency': 'LKR',
};

const Map<String, dynamic> topsPromotionProductsJson = {
  'promotionId': 'promo-1',
  'promotionName': 'Tops 20% Off',
  'hasPriceDiscount': true,
  'products': [
    {
      'productId': 'prod-1',
      'productName': 'Classic Cotton T-Shirt',
      'description': 'Soft combed cotton crew-neck tee.',
      'variants': [tShirtXsOfferJson],
    },
    {
      'productId': 'prod-2',
      'productName': 'Fleece Pullover Hoodie',
      'description': null,
      'variants': [hoodieMOfferJson],
    },
  ],
};

const Map<String, dynamic> freeDeliveryProductsJson = {
  'promotionId': 'promo-2',
  'promotionName': 'Free Delivery',
  'hasPriceDiscount': false,
  'products': <Map<String, dynamic>>[],
};

const Map<String, dynamic> tShirtPromotionsJson = {
  'productId': 'prod-1',
  'productName': 'Classic Cotton T-Shirt',
  'description': 'Soft combed cotton crew-neck tee.',
  'hasActivePromotion': true,
  'promotions': [
    {
      'id': 'promo-1',
      'name': 'Tops 20% Off',
      'description': '20% off all tops.',
      'type': 0,
      'discountValue': 20.0,
      'startDate': '2026-09-01T00:00:00Z',
      'endDate': '2026-12-31T00:00:00Z',
    },
  ],
  'variants': [tShirtXsOfferJson],
};

const Map<String, dynamic> jacketPromotionsJson = {
  'productId': 'prod-3',
  'productName': 'Quilted Field Jacket',
  'description': 'Lightly quilted jacket for layering.',
  'hasActivePromotion': false,
  'promotions': <Map<String, dynamic>>[],
  'variants': [
    {
      'productVariantId': 'var-3',
      'sku': 'JKT-QFD-L',
      'name': 'L / Navy',
      'originalPrice': 12500.0,
      'discountAmount': 0.0,
      'finalPrice': 12500.0,
      'promotionId': null,
      'promotionName': null,
      'currency': 'LKR',
    },
  ],
};

Promotion get topsPromotion => Promotion.fromJson(topsPromotionJson);
Promotion get freeDelivery => Promotion.fromJson(freeDeliveryJson);

/// In-memory [PromotionRepository] for widget tests. Each handler can be
/// replaced per test (e.g. to throw or to wait on a Completer).
class FakePromotionRepository implements PromotionRepository {
  Future<List<Promotion>> Function() onFetchActive =
      () async => [topsPromotion, freeDelivery];

  Future<Promotion> Function(String id) onFetchPromotion = (id) async =>
      id == 'promo-2' ? freeDelivery : topsPromotion;

  Future<PromotionProducts> Function(String id) onFetchPromotionProducts = (id) async =>
      PromotionProducts.fromJson(
        id == 'promo-2' ? freeDeliveryProductsJson : topsPromotionProductsJson,
      );

  Future<ProductPromotions> Function(String productId) onFetchProductPromotions =
      (productId) async => ProductPromotions.fromJson(
            productId == 'prod-3' ? jacketPromotionsJson : tShirtPromotionsJson,
          );

  int activeCalls = 0;

  @override
  Future<List<Promotion>> fetchActivePromotions() {
    activeCalls++;
    return onFetchActive();
  }

  @override
  Future<Promotion> fetchPromotion(String id) => onFetchPromotion(id);

  @override
  Future<PromotionProducts> fetchPromotionProducts(String id) => onFetchPromotionProducts(id);

  @override
  Future<ProductPromotions> fetchProductPromotions(String productId) =>
      onFetchProductPromotions(productId);
}
