import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import '../models/promotion_models.dart';

/// Read-only access to customer promotion data. Screens depend on this
/// interface so tests can supply a fake.
abstract class PromotionRepository {
  /// Promotions that are live now (the API only exposes live promotions to
  /// customers), soonest-ending first.
  Future<List<Promotion>> fetchActivePromotions();

  Future<Promotion> fetchPromotion(String id);

  Future<PromotionProducts> fetchPromotionProducts(String id);

  Future<ProductPromotions> fetchProductPromotions(String productId);
}

class ApiPromotionRepository implements PromotionRepository {
  ApiPromotionRepository(this._client);

  final ApiClient _client;

  @override
  Future<List<Promotion>> fetchActivePromotions() async {
    final json = _object(await _client.getJson(
      '/promotions',
      query: const {'pageSize': '100', 'sortBy': 'endDate', 'sortDirection': 'asc'},
    ));

    return (json['items'] as List<dynamic>? ?? const [])
        .map((item) => Promotion.fromJson(item as Map<String, dynamic>))
        .toList(growable: false);
  }

  @override
  Future<Promotion> fetchPromotion(String id) async =>
      Promotion.fromJson(_object(await _client.getJson('/promotions/${Uri.encodeComponent(id)}')));

  @override
  Future<PromotionProducts> fetchPromotionProducts(String id) async => PromotionProducts.fromJson(
        _object(await _client.getJson('/promotions/${Uri.encodeComponent(id)}/products')),
      );

  @override
  Future<ProductPromotions> fetchProductPromotions(String productId) async =>
      ProductPromotions.fromJson(
        _object(await _client.getJson('/promotions/products/${Uri.encodeComponent(productId)}')),
      );

  static Map<String, dynamic> _object(Object? json) {
    if (json is Map<String, dynamic>) {
      return json;
    }
    throw const ApiException(
      kind: ApiErrorKind.invalidResponse,
      message: 'The server sent an unexpected response.',
    );
  }
}
