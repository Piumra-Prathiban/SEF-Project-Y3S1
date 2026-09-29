import '../../models/shopping_models.dart';
import 'models/promotion_models.dart';

/// The soonest-ending live promotion that applies to [product], either
/// directly or through one of its categories.
///
/// [promotions] should come from [PromotionRepository.fetchActivePromotions],
/// fetched once and reused for every product on screen rather than requested
/// per product. The API already sorts that list soonest-ending first, so the
/// first match is the most urgent offer to surface.
Promotion? promotionForProduct(List<Promotion> promotions, Product product) {
  if (promotions.isEmpty) {
    return null;
  }

  final categoryIds = product.categories.map((category) => category.id);

  for (final promotion in promotions) {
    if (promotion.appliesTo(product.id, categoryIds)) {
      return promotion;
    }
  }

  return null;
}
