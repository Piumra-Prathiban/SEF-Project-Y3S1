import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sef_project/core/api/api_client.dart';
import 'package:sef_project/core/api/api_exception.dart';
import 'package:sef_project/features/promotions/data/promotion_repository.dart';

import '../../support/fixtures.dart';

/// API contract tests: the real ApiClient + repository against responses
/// shaped like the ASP.NET Core Marketing endpoints.
void main() {
  final requests = <Uri>[];

  ApiPromotionRepository repositoryWith(Map<String, Object> routes) {
    requests.clear();
    final client = ApiClient(
      baseUri: Uri.parse('https://api.example.com/api'),
      httpClient: MockClient((request) async {
        requests.add(request.url);
        final body = routes[request.url.path];
        return body == null
            ? http.Response('{"status":404,"title":"Not Found"}', 404)
            : http.Response(
                jsonEncode(body),
                200,
                headers: {'content-type': 'application/json; charset=utf-8'},
              );
      }),
    );
    return ApiPromotionRepository(client);
  }

  test('fetchActivePromotions calls GET /api/promotions sorted by end date', () async {
    final repository = repositoryWith({
      '/api/promotions': {
        'items': [pizzaPromotionJson, freeDeliveryJson],
        'totalCount': 2,
        'page': 1,
        'pageSize': 100,
      },
    });

    final promotions = await repository.fetchActivePromotions();

    expect(promotions.map((p) => p.name), ['Pizza 20% Off', 'Free Delivery']);
    expect(requests.single.queryParameters, {
      'pageSize': '100',
      'sortBy': 'endDate',
      'sortDirection': 'asc',
    });
  });

  test('fetchPromotionProducts calls GET /api/promotions/{id}/products', () async {
    final repository = repositoryWith({
      '/api/promotions/promo-1/products': pizzaPromotionProductsJson,
    });

    final offers = await repository.fetchPromotionProducts('promo-1');

    expect(offers.products.first.variants.single.finalPrice, 960);
  });

  test('fetchProductPromotions calls GET /api/promotions/products/{id}', () async {
    final repository = repositoryWith({
      '/api/promotions/products/prod-1': margheritaPromotionsJson,
    });

    final product = await repository.fetchProductPromotions('prod-1');

    expect(product.hasActivePromotion, isTrue);
    expect(requests.single.path, '/api/promotions/products/prod-1');
  });

  test('a promotion that is not live surfaces as notFound', () async {
    final repository = repositoryWith({});

    await expectLater(
      repository.fetchPromotion('promo-expired'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.notFound)),
    );
  });
}
