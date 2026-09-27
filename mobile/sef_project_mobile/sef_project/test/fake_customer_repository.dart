import 'package:sef_project/models/shopping_models.dart';
import 'package:sef_project/models/order_models.dart';
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
  List<ProductReturn> currentReturns = [];

  Order get currentOrder => Order(
    id: 'order-1',
    orderNumber: 'ORD-TEST-1',
    status: 4,
    placedAt: DateTime.utc(2026, 9, 26),
    subtotal: 4500,
    discountTotal: 0,
    taxAmount: 0,
    shippingFee: 0,
    total: 4500,
    currency: 'LKR',
    items: const [
      OrderItem(
        id: 'order-item-1',
        productVariantId: 'variant-1',
        sku: 'SHIRT-BLUE-M',
        name: 'Linen Shirt',
        quantity: 1,
        unitPrice: 4500,
        lineTotal: 4500,
      ),
    ],
    payments: const [],
    shipments: const [],
    statusHistory: const [],
  );

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

  @override
  Future<OrderList> orders({int page = 1, int pageSize = 20}) async =>
      OrderList(
        items: [
          OrderSummary(
            id: currentOrder.id,
            orderNumber: currentOrder.orderNumber,
            status: currentOrder.status,
            placedAt: currentOrder.placedAt,
            total: currentOrder.total,
            currency: currentOrder.currency,
          ),
        ],
        totalCount: 1,
        page: page,
        pageSize: pageSize,
      );

  @override
  Future<Order> order(String orderId) async => currentOrder;

  @override
  Future<Order> createOrder({
    required List<Map<String, dynamic>> items,
    required Map<String, dynamic> deliveryAddress,
    required int paymentMethod,
    String? couponCode,
  }) async => currentOrder;

  @override
  Future<Order> cancelOrder(String orderId) async => currentOrder;

  @override
  Future<List<ProductReturn>> orderReturns(String orderId) async =>
      List.of(currentReturns);

  @override
  Future<ProductReturn> createReturn(
    String orderId, {
    required int reason,
    required List<Map<String, dynamic>> items,
    String? note,
  }) async {
    final created = ProductReturn(
      id: 'return-1',
      returnNumber: 'RET-TEST-1',
      orderId: orderId,
      status: 0,
      reason: reason,
      refundAmount: 4500,
      currency: 'LKR',
      requestedAt: DateTime.utc(2026, 9, 26),
      items: const [
        ProductReturnItem(
          id: 'return-item-1',
          orderItemId: 'order-item-1',
          name: 'Linen Shirt',
          sku: 'SHIRT-BLUE-M',
          quantity: 1,
          lineRefundAmount: 4500,
        ),
      ],
      customerNote: note,
    );
    currentReturns = [created];
    return created;
  }

  @override
  Future<ProductReturn> cancelReturn(String returnId) async {
    final current = currentReturns.first;
    final cancelled = ProductReturn(
      id: current.id,
      returnNumber: current.returnNumber,
      orderId: current.orderId,
      status: 5,
      reason: current.reason,
      refundAmount: current.refundAmount,
      currency: current.currency,
      requestedAt: current.requestedAt,
      items: current.items,
    );
    currentReturns = [cancelled];
    return cancelled;
  }
}
