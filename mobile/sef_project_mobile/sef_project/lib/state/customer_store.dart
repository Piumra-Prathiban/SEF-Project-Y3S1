import 'package:flutter/widgets.dart';
import '../models/shopping_models.dart';
import '../services/customer_api.dart';

class CustomerStore extends ChangeNotifier {
  CustomerStore(this.api);
  final CustomerRepository api;
  bool initialized = false;
  bool authenticated = false;
  bool loading = false;
  String? error;
  ProductPage products = const ProductPage(items: [], page: 1, totalPages: 0, totalCount: 0);
  List<WishlistItem> wishlist = [];
  Cart cart = Cart.empty();
  CustomerProfile? profile;
  List<CustomerAddress> addresses = [];
  String search = '';
  String? categoryId;
  double? minPrice;
  double? maxPrice;
  bool inStockOnly = false;
  String sortBy = 'name';
  String sortDirection = 'asc';

  Future<void> initialize() async { authenticated = await api.hasToken(); initialized = true; notifyListeners(); }

  Future<void> login(String email, String password) async {
    await _run(() async { await api.login(email, password); authenticated = true; });
  }

  Future<void> logout() async { await api.logout(); authenticated = false; wishlist = []; cart = Cart.empty(); profile = null; addresses = []; notifyListeners(); }

  Future<void> loadProducts({int page = 1}) => _run(() async { products = await api.products(search: search, categoryId: categoryId, minPrice: minPrice, maxPrice: maxPrice, inStockOnly: inStockOnly, sortBy: sortBy, sortDirection: sortDirection, page: page); });

  Future<void> applyFilters({required String searchText, String? category, double? minimum, double? maximum, required bool availableOnly, required String orderBy, required String direction}) async {
    if (minimum != null && maximum != null && minimum > maximum) throw const ApiException('Minimum price cannot exceed maximum price.');
    search = searchText; categoryId = category; minPrice = minimum; maxPrice = maximum; inStockOnly = availableOnly; sortBy = orderBy; sortDirection = direction;
    await loadProducts();
  }

  Future<void> loadWishlist() => _run(() async { wishlist = await api.wishlist(); });
  Future<void> addToWishlist(String productId) => _run(() async { await api.addWishlist(productId); wishlist = await api.wishlist(); });
  Future<void> removeFromWishlist(String productId) => _run(() async { await api.removeWishlist(productId); wishlist.removeWhere((item) => item.productId == productId); });
  Future<Product?> productFor(WishlistItem item) async {
    var pageNumber = 1;
    do {
      final result =
          await api.products(search: item.productName, page: pageNumber);
      for (final product in result.items) {
        if (product.id == item.productId) return product;
      }
      if (pageNumber >= result.totalPages) break;
      pageNumber++;
    } while (true);
    return null;
  }
  Future<void> loadCart() => _run(() async { cart = await api.cart(); });
  Future<void> addToCart(String variantId, int quantity) => _run(() async { if (quantity <= 0) throw const ApiException('Quantity must be greater than zero.'); cart = await api.addCart(variantId, quantity); });
  Future<void> updateCart(String itemId, int quantity) => _run(() async { if (quantity <= 0) throw const ApiException('Quantity must be greater than zero.'); cart = await api.updateCart(itemId, quantity); });
  Future<void> removeCartItem(String itemId) => _run(() async { await api.removeCart(itemId); cart = await api.cart(); });
  Future<void> clearCart() => _run(() async { await api.clearCart(); cart = Cart.empty(); });
  Future<void> loadProfile() => _run(() async { profile = await api.profile(); addresses = await api.addresses(); });
  Future<void> saveProfile(CustomerProfile value) => _run(() async { profile = await api.updateProfile(value); });
  Future<void> saveAddress(CustomerAddress value) => _run(() async { if (value.id == null) { await api.createAddress(value); } else { await api.updateAddress(value); } addresses = await api.addresses(); });
  Future<void> deleteAddress(int id) => _run(() async { await api.deleteAddress(id); addresses = await api.addresses(); });

  Future<void> _run(Future<void> Function() action) async {
    loading = true; error = null; notifyListeners();
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
  const StoreScope({super.key, required CustomerStore store, required super.child}) : super(notifier: store);
  static CustomerStore of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<StoreScope>()!.notifier!;
}
