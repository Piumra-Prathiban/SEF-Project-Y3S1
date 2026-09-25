/// Models for the Marketing API. Field names and enum numbers match the
/// ASP.NET Core DTOs; money values are the server's authoritative figures.
library;

enum PromotionType {
  percentageDiscount,
  fixedAmountDiscount,
  buyXGetY,
  freeShipping,
  unknown;

  static PromotionType fromJson(Object? value) {
    final index = value is int ? value : int.tryParse('$value') ?? -1;
    return index >= 0 && index < PromotionType.unknown.index
        ? PromotionType.values[index]
        : PromotionType.unknown;
  }
}

double _money(Object? value) => (value as num?)?.toDouble() ?? 0;

DateTime _date(Object? value) => DateTime.parse(value as String).toUtc();

List<T> _list<T>(Object? value, T Function(Map<String, dynamic>) parse) =>
    (value as List<dynamic>? ?? const [])
        .map((item) => parse(item as Map<String, dynamic>))
        .toList(growable: false);

/// GET /api/promotions and /api/promotions/{id}
class Promotion {
  const Promotion({
    required this.id,
    required this.name,
    required this.type,
    required this.discountValue,
    required this.startDate,
    required this.endDate,
    this.description,
    this.campaignName,
  });

  factory Promotion.fromJson(Map<String, dynamic> json) => Promotion(
        id: json['id'] as String,
        name: json['name'] as String,
        description: json['description'] as String?,
        type: PromotionType.fromJson(json['type']),
        discountValue: _money(json['discountValue']),
        startDate: _date(json['startDate']),
        endDate: _date(json['endDate']),
        campaignName: json['campaignName'] as String?,
      );

  final String id;
  final String name;
  final String? description;
  final PromotionType type;
  final double discountValue;
  final DateTime startDate;
  final DateTime endDate;
  final String? campaignName;
}

/// A variant with prices calculated by the server.
class VariantOffer {
  const VariantOffer({
    required this.productVariantId,
    required this.sku,
    required this.name,
    required this.originalPrice,
    required this.discountAmount,
    required this.finalPrice,
    required this.currency,
    this.promotionId,
    this.promotionName,
  });

  factory VariantOffer.fromJson(Map<String, dynamic> json) => VariantOffer(
        productVariantId: json['productVariantId'] as String,
        sku: json['sku'] as String,
        name: json['name'] as String,
        originalPrice: _money(json['originalPrice']),
        discountAmount: _money(json['discountAmount']),
        finalPrice: _money(json['finalPrice']),
        currency: json['currency'] as String? ?? 'LKR',
        promotionId: json['promotionId'] as String?,
        promotionName: json['promotionName'] as String?,
      );

  final String productVariantId;
  final String sku;
  final String name;
  final double originalPrice;
  final double discountAmount;
  final double finalPrice;
  final String currency;
  final String? promotionId;
  final String? promotionName;

  bool get isDiscounted => discountAmount > 0;
}

class ProductOffer {
  const ProductOffer({
    required this.productId,
    required this.productName,
    required this.variants,
    this.description,
  });

  factory ProductOffer.fromJson(Map<String, dynamic> json) => ProductOffer(
        productId: json['productId'] as String,
        productName: json['productName'] as String,
        description: json['description'] as String?,
        variants: _list(json['variants'], VariantOffer.fromJson),
      );

  final String productId;
  final String productName;
  final String? description;
  final List<VariantOffer> variants;

  bool get hasDiscount => variants.any((variant) => variant.isDiscounted);
}

/// GET /api/promotions/{id}/products
class PromotionProducts {
  const PromotionProducts({
    required this.promotionId,
    required this.promotionName,
    required this.hasPriceDiscount,
    required this.products,
  });

  factory PromotionProducts.fromJson(Map<String, dynamic> json) => PromotionProducts(
        promotionId: json['promotionId'] as String,
        promotionName: json['promotionName'] as String,
        hasPriceDiscount: json['hasPriceDiscount'] as bool? ?? false,
        products: _list(json['products'], ProductOffer.fromJson),
      );

  final String promotionId;
  final String promotionName;
  final bool hasPriceDiscount;
  final List<ProductOffer> products;
}

class PromotionSummary {
  const PromotionSummary({
    required this.id,
    required this.name,
    required this.type,
    required this.discountValue,
    required this.startDate,
    required this.endDate,
    this.description,
  });

  factory PromotionSummary.fromJson(Map<String, dynamic> json) => PromotionSummary(
        id: json['id'] as String,
        name: json['name'] as String,
        description: json['description'] as String?,
        type: PromotionType.fromJson(json['type']),
        discountValue: _money(json['discountValue']),
        startDate: _date(json['startDate']),
        endDate: _date(json['endDate']),
      );

  final String id;
  final String name;
  final String? description;
  final PromotionType type;
  final double discountValue;
  final DateTime startDate;
  final DateTime endDate;
}

/// GET /api/promotions/products/{productId}
class ProductPromotions {
  const ProductPromotions({
    required this.productId,
    required this.productName,
    required this.hasActivePromotion,
    required this.promotions,
    required this.variants,
    this.description,
  });

  factory ProductPromotions.fromJson(Map<String, dynamic> json) => ProductPromotions(
        productId: json['productId'] as String,
        productName: json['productName'] as String,
        description: json['description'] as String?,
        hasActivePromotion: json['hasActivePromotion'] as bool? ?? false,
        promotions: _list(json['promotions'], PromotionSummary.fromJson),
        variants: _list(json['variants'], VariantOffer.fromJson),
      );

  final String productId;
  final String productName;
  final String? description;
  final bool hasActivePromotion;
  final List<PromotionSummary> promotions;
  final List<VariantOffer> variants;
}
