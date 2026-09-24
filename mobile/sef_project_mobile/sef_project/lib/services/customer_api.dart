import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/shopping_models.dart';
import 'token_storage.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
  @override
  String toString() => message;
}

abstract interface class CustomerRepository {
  Future<bool> hasToken();
  Future<void> login(String email, String password);
  Future<void> logout();
  Future<ProductPage> products({
    String search = '',
    String? categoryId,
    double? minPrice,
    double? maxPrice,
    bool inStockOnly = false,
    String sortBy = 'name',
    String sortDirection = 'asc',
    int page = 1,
  });
  Future<List<WishlistItem>> wishlist();
  Future<void> addWishlist(String productId);
  Future<void> removeWishlist(String productId);
  Future<Cart> cart();
  Future<Cart> addCart(String variantId, int quantity);
  Future<Cart> updateCart(String itemId, int quantity);
  Future<void> removeCart(String itemId);
  Future<void> clearCart();
  Future<RecommendationResult> recommendations(
    RecommendationPreferences preferences,
  );
  Future<CustomerProfile> profile();
  Future<CustomerProfile> updateProfile(CustomerProfile profile);
  Future<List<CustomerAddress>> addresses();
  Future<CustomerAddress> createAddress(CustomerAddress address);
  Future<CustomerAddress> updateAddress(CustomerAddress address);
  Future<void> deleteAddress(int id);
}

class CustomerApi implements CustomerRepository {
  CustomerApi({http.Client? client, TokenStorage? tokens, String? baseUrl})
    : _client = client ?? http.Client(),
      _tokens = tokens ?? const SecureTokenStorage(),
      baseUrl =
          baseUrl ??
          const String.fromEnvironment(
            'API_BASE_URL',
            defaultValue: 'http://10.0.2.2:5193/api',
          );

  final http.Client _client;
  final TokenStorage _tokens;
  final String baseUrl;

  @override
  Future<bool> hasToken() async => (await _tokens.read())?.isNotEmpty == true;

  @override
  Future<void> login(String email, String password) async {
    final data = await _request(
      'POST',
      '/Auth/login',
      authenticated: false,
      body: {'email': email, 'password': password},
    ) as Map<String, dynamic>;
    final token = data['token'] as String?;
    if (token == null || token.isEmpty) {
      throw const ApiException('The server did not return an access token.');
    }
    await _tokens.write(token);
  }

  @override
  Future<void> logout() => _tokens.clear();

  @override
  Future<ProductPage> products({
    String search = '',
    String? categoryId,
    double? minPrice,
    double? maxPrice,
    bool inStockOnly = false,
    String sortBy = 'name',
    String sortDirection = 'asc',
    int page = 1,
  }) async {
    final query = <String, String>{
      'page': '$page',
      'pageSize': '12',
      'sortBy': sortBy,
      'sortDirection': sortDirection,
    };
    if (search.trim().isNotEmpty) query['search'] = search.trim();
    if (categoryId?.isNotEmpty == true) query['categoryId'] = categoryId!;
    if (minPrice != null) query['minPrice'] = '$minPrice';
    if (maxPrice != null) query['maxPrice'] = '$maxPrice';
    if (inStockOnly) query['inStockOnly'] = 'true';
    final uri = Uri.parse('$baseUrl/shopping/products')
        .replace(queryParameters: query);
    return ProductPage.fromJson(
      await _requestUri('GET', uri, authenticated: false)
          as Map<String, dynamic>,
    );
  }

  @override
  Future<List<WishlistItem>> wishlist() async =>
      ((await _request('GET', '/wishlist') as Map<String, dynamic>)['items']
                  as List? ??
              [])
          .map((item) => WishlistItem.fromJson(item as Map<String, dynamic>))
          .toList();
  @override
  Future<void> addWishlist(String productId) async {
    await _request('POST', '/wishlist/items', body: {'productId': productId});
  }

  @override
  Future<void> removeWishlist(String productId) async {
    await _request('DELETE', '/wishlist/items/$productId');
  }

  @override
  Future<Cart> cart() async =>
      Cart.fromJson(await _request('GET', '/cart') as Map<String, dynamic>);
  @override
  Future<Cart> addCart(String variantId, int quantity) async => Cart.fromJson(
    await _request(
      'POST',
      '/cart/items',
      body: {'productVariantId': variantId, 'quantity': quantity},
    ) as Map<String, dynamic>,
  );
  @override
  Future<Cart> updateCart(String itemId, int quantity) async => Cart.fromJson(
    await _request('PUT', '/cart/items/$itemId', body: {'quantity': quantity})
        as Map<String, dynamic>,
  );
  @override
  Future<void> removeCart(String itemId) async {
    await _request('DELETE', '/cart/items/$itemId');
  }

  @override
  Future<void> clearCart() async {
    await _request('DELETE', '/cart');
  }

  @override
  Future<RecommendationResult> recommendations(
    RecommendationPreferences preferences,
  ) async {
    final data = await _request(
      'POST',
      '/recommendations',
      body: preferences.toJson(),
    ).timeout(const Duration(seconds: 30));
    return RecommendationResult.fromJson(data as Map<String, dynamic>);
  }

  @override
  Future<CustomerProfile> profile() async => CustomerProfile.fromJson(
    await _request('GET', '/profile') as Map<String, dynamic>,
  );
  @override
  Future<CustomerProfile> updateProfile(CustomerProfile profile) async =>
      CustomerProfile.fromJson(
        await _request(
          'PUT',
          '/profile',
          body: {
            'email': profile.email,
            'firstName': profile.firstName,
            'lastName': profile.lastName,
          },
        ) as Map<String, dynamic>,
      );
  @override
  Future<List<CustomerAddress>> addresses() async =>
      (await _request('GET', '/profile/addresses') as List)
          .map((item) => CustomerAddress.fromJson(item as Map<String, dynamic>))
          .toList();
  @override
  Future<CustomerAddress> createAddress(CustomerAddress address) async =>
      CustomerAddress.fromJson(
        await _request('POST', '/profile/addresses', body: address.toJson())
            as Map<String, dynamic>,
      );
  @override
  Future<CustomerAddress> updateAddress(CustomerAddress address) async =>
      CustomerAddress.fromJson(
        await _request(
          'PUT',
          '/profile/addresses/${address.id}',
          body: address.toJson(),
        ) as Map<String, dynamic>,
      );
  @override
  Future<void> deleteAddress(int id) async {
    await _request('DELETE', '/profile/addresses/$id');
  }

  Future<dynamic> _request(
    String method,
    String path, {
    bool authenticated = true,
    Map<String, dynamic>? body,
  }) => _requestUri(
    method,
    Uri.parse('$baseUrl$path'),
    authenticated: authenticated,
    body: body,
  );

  Future<dynamic> _requestUri(
    String method,
    Uri uri, {
    bool authenticated = true,
    Map<String, dynamic>? body,
  }) async {
    final headers = <String, String>{'Content-Type': 'application/json'};
    if (authenticated) {
      final token = await _tokens.read();
      if (token == null) {
        throw const ApiException('Please log in to continue.', statusCode: 401);
      }
      headers['Authorization'] = 'Bearer $token';
    }
    late http.Response response;
    final encoded = body == null ? null : jsonEncode(body);
    switch (method) {
      case 'POST':
        response = await _client.post(uri, headers: headers, body: encoded);
        break;
      case 'PUT':
        response = await _client.put(uri, headers: headers, body: encoded);
        break;
      case 'DELETE':
        response = await _client.delete(uri, headers: headers);
        break;
      default:
        response = await _client.get(uri, headers: headers);
        break;
    }
    dynamic data;
    if (response.body.isNotEmpty) {
      try {
        data = jsonDecode(response.body);
      } on FormatException {
        data = null;
      }
    }
    if (response.statusCode < 200 || response.statusCode >= 300) {
      final detail = data is Map<String, dynamic>
          ? data['detail'] ?? data['title']
          : null;
      throw ApiException(
        detail?.toString() ?? 'The request could not be completed.',
        statusCode: response.statusCode,
      );
    }
    return data;
  }
}
