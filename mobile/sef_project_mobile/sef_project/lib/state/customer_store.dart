import 'dart:async';

import 'package:flutter/widgets.dart';

import '../models/shopping_models.dart';
import '../services/customer_api.dart';

enum RecommendationUiStatus {
  idle,
  pending,
  processing,
  completed,
  validationFailure,
  safeFailure,
  timeout,
  error,
}

class CustomerStore extends ChangeNotifier {
  CustomerStore(this.api);
  final CustomerRepository api;
  bool initialized = false;
  bool authenticated = false;
  bool loading = false;
  String? error;
  ProductPage products = const ProductPage(
    items: [],
    page: 1,
    totalPages: 0,
    totalCount: 0,
  );
  List<WishlistItem> wishlist = [];
  Cart cart = Cart.empty();
  CustomerProfile? profile;
  List<CustomerAddress> addresses = [];
  RecommendationUiStatus recommendationStatus = RecommendationUiStatus.idle;
  RecommendationResult? recommendation;
  String? recommendationMessage;
  final Map<String, Product> recommendationProducts = {};
  String search = '';
  String? categoryId;
  double? minPrice;
  double? maxPrice;
  bool inStockOnly = false;
  String sortBy = 'name';
  String sortDirection = 'asc';

  Future<void> initialize() async {
    authenticated = await api.hasToken();
    initialized = true;
    notifyListeners();
  }

  Future<void> login(String email, String password) async {
    await _run(() async {
      await api.login(email, password);
      authenticated = true;
    });
  }

  Future<void> logout() async {
    await api.logout();
    authenticated = false;
    wishlist = [];
    cart = Cart.empty();
    profile = null;
    addresses = [];
    recommendation = null;
    recommendationProducts.clear();
    recommendationStatus = RecommendationUiStatus.idle;
    recommendationMessage = null;
    notifyListeners();
  }

  Future<void> loadProducts({int page = 1}) => _run(() async {
    products = await api.products(
      search: search,
      categoryId: categoryId,
      minPrice: minPrice,
      maxPrice: maxPrice,
      inStockOnly: inStockOnly,
      sortBy: sortBy,
      sortDirection: sortDirection,
      page: page,
    );
  });

  Future<void> applyFilters({
    required String searchText,
    String? category,
    double? minimum,
    double? maximum,
    required bool availableOnly,
    required String orderBy,
    required String direction,
  }) async {
    if ((minimum != null && (!minimum.isFinite || minimum < 0)) ||
        (maximum != null && (!maximum.isFinite || maximum < 0))) {
      throw const ApiException('Prices must be valid non-negative numbers.');
    }
    if (minimum != null && maximum != null && minimum > maximum) {
      throw const ApiException('Minimum price cannot exceed maximum price.');
    }
    search = searchText;
    categoryId = category;
    minPrice = minimum;
    maxPrice = maximum;
    inStockOnly = availableOnly;
    sortBy = orderBy;
    sortDirection = direction;
    await loadProducts();
  }

  Future<void> loadWishlist() => _run(() async {
    wishlist = await api.wishlist();
  });
  Future<void> addToWishlist(String productId) => _run(() async {
    await api.addWishlist(productId);
    wishlist = await api.wishlist();
  });
  Future<void> removeFromWishlist(String productId) => _run(() async {
    await api.removeWishlist(productId);
    wishlist.removeWhere((item) => item.productId == productId);
  });
  Future<Product?> productFor(WishlistItem item) async {
    var pageNumber = 1;
    do {
      final result = await api.products(
        search: item.productName,
        page: pageNumber,
      );
      for (final product in result.items) {
        if (product.id == item.productId) return product;
      }
      if (pageNumber >= result.totalPages) break;
      pageNumber++;
    } while (true);
    return null;
  }

  Future<void> loadCart() => _run(() async {
    cart = await api.cart();
  });
  Future<void> addToCart(String variantId, int quantity) => _run(() async {
    if (quantity <= 0) {
      throw const ApiException('Quantity must be greater than zero.');
    }
    cart = await api.addCart(variantId, quantity);
  });
  Future<void> updateCart(String itemId, int quantity) => _run(() async {
    if (quantity <= 0) {
      throw const ApiException('Quantity must be greater than zero.');
    }
    cart = await api.updateCart(itemId, quantity);
  });
  Future<void> removeCartItem(String itemId) => _run(() async {
    await api.removeCart(itemId);
    cart = await api.cart();
  });
  Future<void> clearCart() => _run(() async {
    await api.clearCart();
    cart = Cart.empty();
  });
  Future<void> requestRecommendations(
    RecommendationPreferences preferences,
  ) async {
    _validateRecommendationPreferences(preferences);
    recommendation = null;
    recommendationProducts.clear();
    recommendationMessage = null;
    recommendationStatus = RecommendationUiStatus.pending;
    notifyListeners();

    await Future<void>.delayed(Duration.zero);
    recommendationStatus = RecommendationUiStatus.processing;
    notifyListeners();

    try {
      final result = await api.recommendations(preferences);
      recommendation = result;
      recommendationStatus = _recommendationStatusFor(result);
      recommendationMessage = result.execution.errorSummary;

      if (recommendationStatus == RecommendationUiStatus.completed) {
        await _loadRecommendationProducts(result.recommendations);
      }
    } on TimeoutException {
      recommendationStatus = RecommendationUiStatus.timeout;
      recommendationMessage =
          'The recommendation workflow timed out. Please try again.';
    } catch (exception) {
      recommendationStatus = RecommendationUiStatus.error;
      recommendationMessage = exception.toString();
      if (exception is ApiException && exception.statusCode == 401) {
        await api.logout();
        authenticated = false;
      }
    } finally {
      notifyListeners();
    }
  }

  void resetRecommendations() {
    recommendation = null;
    recommendationProducts.clear();
    recommendationMessage = null;
    recommendationStatus = RecommendationUiStatus.idle;
    notifyListeners();
  }

  Product? recommendationProduct(String productId) =>
      recommendationProducts[productId];
  Future<void> loadProfile() => _run(() async {
    profile = await api.profile();
    addresses = await api.addresses();
  });
  Future<void> saveProfile(CustomerProfile value) => _run(() async {
    profile = await api.updateProfile(value);
  });
  Future<void> saveAddress(CustomerAddress value) => _run(() async {
    if (value.id == null) {
      await api.createAddress(value);
    } else {
      await api.updateAddress(value);
    }
    addresses = await api.addresses();
  });
  Future<void> deleteAddress(int id) => _run(() async {
    await api.deleteAddress(id);
    addresses = await api.addresses();
  });

  Future<void> _loadRecommendationProducts(
    List<ProductRecommendation> items,
  ) async {
    for (final item in items) {
      try {
        final product = await _findProduct(item.productId, item.productName);
        if (product != null) recommendationProducts[item.productId] = product;
      } catch (_) {
        // The grounded recommendation remains usable if optional image/detail
        // hydration is temporarily unavailable.
      }
    }
  }

  Future<Product?> _findProduct(String productId, String productName) async {
    var pageNumber = 1;
    do {
      final result = await api.products(search: productName, page: pageNumber);
      for (final product in result.items) {
        if (product.id == productId) return product;
      }
      if (pageNumber >= result.totalPages) break;
      pageNumber++;
    } while (true);
    return null;
  }

  static RecommendationUiStatus _recommendationStatusFor(
    RecommendationResult result,
  ) {
    final status = result.status.toLowerCase();
    if (status == 'pending') return RecommendationUiStatus.pending;
    if (status == 'planning' ||
        status == 'inprogress' ||
        status == 'processing') {
      return RecommendationUiStatus.processing;
    }
    if (status == 'completed' && result.execution.outputValidated) {
      return RecommendationUiStatus.completed;
    }

    final error = result.execution.errorSummary?.toLowerCase() ?? '';
    if (error.contains('timed out') || error.contains('timeout')) {
      return RecommendationUiStatus.timeout;
    }
    if (result.execution.validationResults.any((item) => !item.isValid) ||
        error.contains('validation')) {
      return RecommendationUiStatus.validationFailure;
    }
    return RecommendationUiStatus.safeFailure;
  }

  static void _validateRecommendationPreferences(
    RecommendationPreferences preferences,
  ) {
    final occasion = preferences.occasion.trim();
    if (occasion.length < 2 || occasion.length > 100) {
      throw const ApiException(
        'Enter an occasion between 2 and 100 characters.',
      );
    }
    if (preferences.budget != null &&
        (preferences.budget! <= 0 || preferences.budget! > 999999999)) {
      throw const ApiException('Budget must be a positive valid price.');
    }
    if (preferences.preferredColours.length > 10 ||
        preferences.preferredColours.any(
          (item) => item.trim().isEmpty || item.trim().length > 50,
        )) {
      throw const ApiException('Enter up to 10 valid preferred colours.');
    }
    if ((preferences.preferredSize?.trim().length ?? 0) > 50) {
      throw const ApiException(
        'Preferred size must be 50 characters or fewer.',
      );
    }
    if ((preferences.stylePreferences?.trim().length ?? 0) > 500) {
      throw const ApiException(
        'Style preferences must be 500 characters or fewer.',
      );
    }
  }

  Future<void> _run(Future<void> Function() action) async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      await action();
    } catch (exception) {
      error = exception.toString();
      if (exception is ApiException && exception.statusCode == 401) {
        await api.logout();
        authenticated = false;
      }
      rethrow;
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}

class StoreScope extends InheritedNotifier<CustomerStore> {
  const StoreScope({
    super.key,
    required CustomerStore store,
    required super.child,
  }) : super(notifier: store);
  static CustomerStore of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<StoreScope>()!.notifier!;
}
