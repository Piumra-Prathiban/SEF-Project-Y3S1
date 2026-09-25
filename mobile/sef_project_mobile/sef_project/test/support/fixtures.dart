import 'package:sef_project/features/promotions/data/promotion_repository.dart';
import 'package:sef_project/features/promotions/models/promotion_models.dart';

// JSON shaped exactly like the ASP.NET Core Marketing API responses.

const Map<String, dynamic> pizzaPromotionJson = {
  'id': 'promo-1',
  'campaignId': 'camp-1',
  'campaignName': 'Summer Launch',
  'name': 'Pizza 20% Off',
  'description': '20% off all pizzas.',
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

const Map<String, dynamic> margheritaSmallOfferJson = {
  'productVariantId': 'var-1',
  'sku': 'PIZ-MARG-S',
  'name': 'Small',
  'originalPrice': 1200.0,
  'discountAmount': 240.0,
  'finalPrice': 960.0,
  'promotionId': 'promo-1',
  'promotionName': 'Pizza 20% Off',
  'currency': 'LKR',
};

const Map<String, dynamic> garlicBreadOfferJson = {
  'productVariantId': 'var-9',
  'sku': 'SID-GARL-R',
  'name': 'Regular',
  'originalPrice': 500.0,
  'discountAmount': 0.0,
  'finalPrice': 500.0,
  'promotionId': null,
  'promotionName': null,
  'currency': 'LKR',
};

const Map<String, dynamic> pizzaPromotionProductsJson = {
  'promotionId': 'promo-1',
  'promotionName': 'Pizza 20% Off',
  'hasPriceDiscount': true,
  'products': [
    {
      'productId': 'prod-1',
      'productName': 'Margherita Pizza',
      'description': 'Classic tomato, mozzarella and basil.',
      'variants': [margheritaSmallOfferJson],
    },
    {
      'productId': 'prod-9',
      'productName': 'Garlic Bread',
      'description': null,
      'variants': [garlicBreadOfferJson],
    },
  ],
};

const Map<String, dynamic> freeDeliveryProductsJson = {
  'promotionId': 'promo-2',
  'promotionName': 'Free Delivery',
  'hasPriceDiscount': false,
  'products': <Map<String, dynamic>>[],
};

const Map<String, dynamic> margheritaPromotionsJson = {
  'productId': 'prod-1',
  'productName': 'Margherita Pizza',
  'description': 'Classic tomato, mozzarella and basil.',
  'hasActivePromotion': true,
  'promotions': [
    {
      'id': 'promo-1',
      'name': 'Pizza 20% Off',
      'description': '20% off all pizzas.',
      'type': 0,
      'discountValue': 20.0,
      'startDate': '2026-09-01T00:00:00Z',
      'endDate': '2026-12-31T00:00:00Z',
    },
  ],
  'variants': [margheritaSmallOfferJson],
};

const Map<String, dynamic> carbonaraPromotionsJson = {
  'productId': 'prod-3',
  'productName': 'Spaghetti Carbonara',
  'description': 'Creamy pasta with pancetta.',
  'hasActivePromotion': false,
  'promotions': <Map<String, dynamic>>[],
  'variants': [
    {
      'productVariantId': 'var-3',
      'sku': 'PST-CARB-R',
      'name': 'Regular',
      'originalPrice': 1800.0,
      'discountAmount': 0.0,
      'finalPrice': 1800.0,
      'promotionId': null,
      'promotionName': null,
      'currency': 'LKR',
    },
  ],
};

Promotion get pizzaPromotion => Promotion.fromJson(pizzaPromotionJson);
Promotion get freeDelivery => Promotion.fromJson(freeDeliveryJson);

/// In-memory [PromotionRepository] for widget tests. Each handler can be
/// replaced per test (e.g. to throw or to wait on a Completer).
class FakePromotionRepository implements PromotionRepository {
  Future<List<Promotion>> Function() onFetchActive =
      () async => [pizzaPromotion, freeDelivery];

  Future<Promotion> Function(String id) onFetchPromotion = (id) async =>
      id == 'promo-2' ? freeDelivery : pizzaPromotion;

  Future<PromotionProducts> Function(String id) onFetchPromotionProducts = (id) async =>
      PromotionProducts.fromJson(
        id == 'promo-2' ? freeDeliveryProductsJson : pizzaPromotionProductsJson,
      );

  Future<ProductPromotions> Function(String productId) onFetchProductPromotions =
      (productId) async => ProductPromotions.fromJson(
            productId == 'prod-3' ? carbonaraPromotionsJson : margheritaPromotionsJson,
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
