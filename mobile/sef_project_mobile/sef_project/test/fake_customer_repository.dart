import 'package:sef_project/models/shopping_models.dart';
import 'package:sef_project/services/customer_api.dart';

class FakeCustomerRepository implements CustomerRepository {
  bool token = true;
  String? lastSearch;
  String? lastCategoryId;
  double? lastMinPrice;
  double? lastMaxPrice;
  bool lastInStockOnly = false;
  String? lastSortBy;
  String? lastSortDirection;
  int lastPage = 1;
  RecommendationPreferences? lastRecommendationPreferences;
  Object? productError;
  Object? recommendationError;
  Duration recommendationDelay = Duration.zero;

  ProductPage productPage = const ProductPage(
    items: [
      Product(
        id: 'product-1',
        name: 'Linen Shirt',
        description: 'A lightweight shirt.',
        minimumPrice: 4500,
        isAvailable: true,
        categories: [Category(id: 'category-1', name: 'Shirts')],
        variants: [
          ProductVariant(
            id: 'variant-1',
            sku: 'SHIRT-BLUE-M',
            name: 'Blue / Medium',
            price: 4500,
            availableQuantity: 5,
            isAvailable: true,
            size: 'M',
            colour: 'Blue',
          ),
        ],
      ),
    ],
    page: 1,
    totalPages: 1,
    totalCount: 1,
  );

  List<WishlistItem> wishlistItems = [
    const WishlistItem(
      productId: 'product-1',
      productName: 'Linen Shirt',
      minimumPrice: 4500,
      isAvailable: true,
    ),
  ];

  Cart currentCart = Cart.empty();
  RecommendationResult recommendationResult = const RecommendationResult(
    workflowId: 'workflow-1',
    status: 'completed',
    recommendations: [
      ProductRecommendation(
        productId: 'product-1',
        variantId: 'variant-1',
        productName: 'Linen Shirt',
        variantName: 'Blue / Medium',
        sku: 'SHIRT-BLUE-M',
        price: 4500,
        quantity: 1,
        availableQuantity: 5,
        size: 'M',
        colour: 'Blue',
        reason: 'Matches the requested dinner style and budget.',
      ),
    ],
    execution: RecommendationExecution(
      agentName: 'Personal Stylist Agent',
      status: 'completed',
      outputValidated: true,
      validationResults: [
        RecommendationValidation(
          rule: 'Price',
          isValid: true,
          message: 'Price verified.',
        ),
      ],
    ),
  );
  CustomerProfile currentProfile = const CustomerProfile(
    email: 'customer@example.com',
    firstName: 'Sam',
    lastName: 'Perera',
  );
  List<CustomerAddress> currentAddresses = [
    const CustomerAddress(
      id: 1,
      label: 'Home',
      addressLine1: '10 Main Street',
      city: 'Colombo',
      postalCode: '00100',
      country: 'Sri Lanka',
      isDefault: true,
    ),
  ];

  @override
  Future<bool> hasToken() async => token;

  @override
  Future<void> login(String email, String password) async {
    token = true;
  }

  @override
  Future<void> logout() async {
    token = false;
  }

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
    if (productError != null) throw productError!;
    lastSearch = search;
    lastCategoryId = categoryId;
    lastMinPrice = minPrice;
    lastMaxPrice = maxPrice;
    lastInStockOnly = inStockOnly;
    lastSortBy = sortBy;
    lastSortDirection = sortDirection;
    lastPage = page;
    return productPage;
  }

  @override
  Future<List<WishlistItem>> wishlist() async => List.of(wishlistItems);

  @override
  Future<void> addWishlist(String productId) async {
    if (wishlistItems.any((item) => item.productId == productId)) return;
    final product = productPage.items.firstWhere(
      (item) => item.id == productId,
    );
    wishlistItems.add(
      WishlistItem(
        productId: product.id,
        productName: product.name,
        description: product.description,
        minimumPrice: product.minimumPrice,
        isAvailable: product.isAvailable,
      ),
    );
  }

  @override
  Future<void> removeWishlist(String productId) async {
    wishlistItems.removeWhere((item) => item.productId == productId);
  }

  @override
  Future<Cart> cart() async => currentCart;

  @override
  Future<Cart> addCart(String variantId, int quantity) async {
    final product = productPage.items.first;
    final variant = product.variants.firstWhere((item) => item.id == variantId);
    final existing = currentCart.items.where(
      (item) => item.productVariantId == variantId,
    );
    final totalQuantity =
        quantity + (existing.isEmpty ? 0 : existing.first.quantity);
    currentCart = _cartWith(totalQuantity, product, variant);
    return currentCart;
  }

  @override
  Future<Cart> updateCart(String itemId, int quantity) async {
    final product = productPage.items.first;
    final variant = product.variants.first;
    currentCart = _cartWith(quantity, product, variant);
    return currentCart;
  }

  @override
  Future<void> removeCart(String itemId) async {
    currentCart = Cart.empty();
  }

  @override
  Future<void> clearCart() async {
    currentCart = Cart.empty();
  }

  @override
  Future<RecommendationResult> recommendations(
    RecommendationPreferences preferences,
  ) async {
    lastRecommendationPreferences = preferences;
    if (recommendationDelay > Duration.zero) {
      await Future<void>.delayed(recommendationDelay);
    }
    if (recommendationError != null) throw recommendationError!;
    return recommendationResult;
  }

  Cart _cartWith(int quantity, Product product, ProductVariant variant) {
    final lineTotal = variant.price * quantity;
    return Cart(
      items: [
        CartItem(
          id: 'cart-item-1',
          productVariantId: variant.id,
          productName: product.name,
          variantName: variant.name,
          sku: variant.sku,
          unitPrice: variant.price,
          quantity: quantity,
          lineTotal: lineTotal,
          availableQuantity: variant.availableQuantity,
          hasSufficientStock: quantity <= variant.availableQuantity,
        ),
      ],
      currency: 'LKR',
      totalQuantity: quantity,
      subtotal: lineTotal,
      total: lineTotal,
    );
  }

  @override
  Future<CustomerProfile> profile() async => currentProfile;

  @override
  Future<CustomerProfile> updateProfile(CustomerProfile profile) async {
    currentProfile = profile;
    return profile;
  }

  @override
  Future<List<CustomerAddress>> addresses() async => List.of(currentAddresses);

  @override
  Future<CustomerAddress> createAddress(CustomerAddress address) async {
    final created = CustomerAddress(
      id: currentAddresses.length + 1,
      label: address.label,
      addressLine1: address.addressLine1,
      addressLine2: address.addressLine2,
      city: address.city,
      province: address.province,
      postalCode: address.postalCode,
      country: address.country,
      isDefault: address.isDefault,
    );
    _setAddress(created);
    return created;
  }

  @override
  Future<CustomerAddress> updateAddress(CustomerAddress address) async {
    _setAddress(address);
    return address;
  }

  void _setAddress(CustomerAddress address) {
    if (address.isDefault) {
      currentAddresses = currentAddresses
          .map(
            (item) => CustomerAddress(
              id: item.id,
              label: item.label,
              addressLine1: item.addressLine1,
              addressLine2: item.addressLine2,
              city: item.city,
              province: item.province,
              postalCode: item.postalCode,
              country: item.country,
              isDefault: false,
            ),
          )
          .toList();
    }
    currentAddresses.removeWhere((item) => item.id == address.id);
    currentAddresses.add(address);
  }

  @override
  Future<void> deleteAddress(int id) async {
    currentAddresses.removeWhere((item) => item.id == id);
  }
}
