import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sef_project/services/customer_api.dart';
import 'package:sef_project/services/token_storage.dart';

class _MemoryTokenStorage implements TokenStorage {
  String? token;
  @override
  Future<void> clear() async { token = null; }
  @override
  Future<String?> read() async => token;
  @override
  Future<void> write(String token) async { this.token = token; }
}

void main() {
  test('product discovery sends server-side filters as query parameters',
      () async {
    late Uri requestedUri;
    final client = MockClient((request) async {
      requestedUri = request.url;
      return http.Response(jsonEncode({
        'items': <Object>[],
        'page': 2,
        'totalPages': 3,
        'totalCount': 30,
      }), 200);
    });
    final api = CustomerApi(
      client: client,
      tokens: _MemoryTokenStorage(),
      baseUrl: 'https://example.test/api',
    );

    final result = await api.products(
      search: 'linen shirt',
      categoryId: 'category-id',
      minPrice: 1000,
      maxPrice: 5000,
      inStockOnly: true,
      sortBy: 'price',
      sortDirection: 'desc',
      page: 2,
    );

    expect(requestedUri.path, '/api/shopping/products');
    expect(requestedUri.queryParameters['search'], 'linen shirt');
    expect(requestedUri.queryParameters['categoryId'], 'category-id');
    expect(requestedUri.queryParameters['inStockOnly'], 'true');
    expect(requestedUri.queryParameters['sortBy'], 'price');
    expect(requestedUri.queryParameters['page'], '2');
    expect(result.page, 2);
  });

  test('authenticated requests attach the stored bearer token', () async {
    final tokens = _MemoryTokenStorage()..token = 'test-token';
    late String? authorization;
    final client = MockClient((request) async {
      authorization = request.headers['authorization'];
      return http.Response(jsonEncode({
        'items': <Object>[],
        'currency': 'LKR',
        'totalQuantity': 0,
        'subtotal': 0,
        'total': 0,
      }), 200);
    });
    final api = CustomerApi(
      client: client,
      tokens: tokens,
      baseUrl: 'https://example.test/api',
    );

    await api.cart();

    expect(authorization, 'Bearer test-token');
  });

  test('problem details become a safe ApiException', () async {
    final client = MockClient((request) async => http.Response(
          jsonEncode({'title': 'Not authorized'}),
          401,
        ));
    final tokens = _MemoryTokenStorage()..token = 'expired';
    final api = CustomerApi(
      client: client,
      tokens: tokens,
      baseUrl: 'https://example.test/api',
    );

    await expectLater(
      api.cart(),
      throwsA(isA<ApiException>()
          .having((error) => error.statusCode, 'statusCode', 401)),
    );
  });
}
