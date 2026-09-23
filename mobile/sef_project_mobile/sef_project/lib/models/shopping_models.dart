class ProductPage {
  const ProductPage({required this.items, required this.page, required this.totalPages, required this.totalCount});
  final List<Product> items;
  final int page;
  final int totalPages;
  final int totalCount;

  factory ProductPage.fromJson(Map<String, dynamic> json) => ProductPage(
    items: (json['items'] as List? ?? []).map((item) => Product.fromJson(item as Map<String, dynamic>)).toList(),
    page: json['page'] as int? ?? 1,
    totalPages: json['totalPages'] as int? ?? 0,
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class Product {
  const Product({required this.id, required this.name, required this.minimumPrice, required this.isAvailable, required this.variants, this.description, this.categories = const [], this.images = const []});
  final String id;
  final String name;
  final String? description;
  final double minimumPrice;
  final bool isAvailable;
  final List<ProductVariant> variants;
  final List<Category> categories;
  final List<String> images;

  factory Product.fromJson(Map<String, dynamic> json) => Product(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    description: json['description'] as String?,
    minimumPrice: (json['minimumPrice'] as num?)?.toDouble() ?? 0,
    isAvailable: json['isAvailable'] as bool? ?? false,
    variants: (json['variants'] as List? ?? []).map((item) => ProductVariant.fromJson(item as Map<String, dynamic>)).toList(),
    categories: (json['categories'] as List? ?? []).map((item) => Category.fromJson(item as Map<String, dynamic>)).toList(),
    images: (json['images'] as List? ?? []).map((item) => item.toString()).toList(),
  );
}

class ProductVariant {
  const ProductVariant({required this.id, required this.sku, required this.name, required this.price, required this.availableQuantity, required this.isAvailable, this.size, this.colour});
  final String id;
  final String sku;
  final String name;
  final double price;
  final int availableQuantity;
  final bool isAvailable;
  final String? size;
  final String? colour;

  factory ProductVariant.fromJson(Map<String, dynamic> json) => ProductVariant(
    id: json['id'] as String,
    sku: json['sku'] as String? ?? '',
    name: json['name'] as String? ?? '',
    price: (json['price'] as num?)?.toDouble() ?? 0,
    availableQuantity: json['availableQuantity'] as int? ?? 0,
    isAvailable: json['isAvailable'] as bool? ?? false,
    size: json['size']?.toString(),
    colour: json['colour']?.toString(),
  );
}

class Category {
  const Category({required this.id, required this.name});
  final String id;
  final String name;
  factory Category.fromJson(Map<String, dynamic> json) => Category(id: json['id'] as String, name: json['name'] as String? ?? '');
}

class WishlistItem {
  const WishlistItem({required this.productId, required this.productName, required this.isAvailable, this.description, this.minimumPrice});
  final String productId;
  final String productName;
  final String? description;
  final double? minimumPrice;
  final bool isAvailable;
  factory WishlistItem.fromJson(Map<String, dynamic> json) => WishlistItem(
    productId: json['productId'] as String,
    productName: json['productName'] as String? ?? '',
    description: json['description'] as String?,
    minimumPrice: (json['minimumPrice'] as num?)?.toDouble(),
    isAvailable: json['isAvailable'] as bool? ?? false,
  );
}

class Cart {
  const Cart({required this.items, required this.currency, required this.totalQuantity, required this.subtotal, required this.total});
  final List<CartItem> items;
  final String currency;
  final int totalQuantity;
  final double subtotal;
  final double total;
  factory Cart.empty() => const Cart(items: [], currency: 'LKR', totalQuantity: 0, subtotal: 0, total: 0);
  factory Cart.fromJson(Map<String, dynamic> json) => Cart(
    items: (json['items'] as List? ?? []).map((item) => CartItem.fromJson(item as Map<String, dynamic>)).toList(),
    currency: json['currency'] as String? ?? 'LKR',
    totalQuantity: json['totalQuantity'] as int? ?? 0,
    subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
    total: (json['total'] as num?)?.toDouble() ?? 0,
  );
}

class CartItem {
  const CartItem({required this.id, required this.productVariantId, required this.productName, required this.variantName, required this.sku, required this.unitPrice, required this.quantity, required this.lineTotal, required this.availableQuantity, required this.hasSufficientStock});
  final String id;
  final String productVariantId;
  final String productName;
  final String variantName;
  final String sku;
  final double unitPrice;
  final int quantity;
  final double lineTotal;
  final int availableQuantity;
  final bool hasSufficientStock;
  factory CartItem.fromJson(Map<String, dynamic> json) => CartItem(
    id: json['id'] as String,
    productVariantId: json['productVariantId'] as String,
    productName: json['productName'] as String? ?? '',
    variantName: json['variantName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    unitPrice: (json['unitPrice'] as num?)?.toDouble() ?? 0,
    quantity: json['quantity'] as int? ?? 0,
    lineTotal: (json['lineTotal'] as num?)?.toDouble() ?? 0,
    availableQuantity: json['availableQuantity'] as int? ?? 0,
    hasSufficientStock: json['hasSufficientStock'] as bool? ?? false,
  );
}

class CustomerProfile {
  const CustomerProfile({required this.email, required this.firstName, required this.lastName});
  final String email;
  final String firstName;
  final String lastName;
  factory CustomerProfile.fromJson(Map<String, dynamic> json) => CustomerProfile(email: json['email'] as String? ?? '', firstName: json['firstName'] as String? ?? '', lastName: json['lastName'] as String? ?? '');
}

class CustomerAddress {
  const CustomerAddress({this.id, required this.label, required this.addressLine1, this.addressLine2, required this.city, this.province, required this.postalCode, required this.country, required this.isDefault});
  final int? id;
  final String label;
  final String addressLine1;
  final String? addressLine2;
  final String city;
  final String? province;
  final String postalCode;
  final String country;
  final bool isDefault;
  factory CustomerAddress.fromJson(Map<String, dynamic> json) => CustomerAddress(id: json['id'] as int?, label: json['label'] as String? ?? '', addressLine1: json['addressLine1'] as String? ?? '', addressLine2: json['addressLine2'] as String?, city: json['city'] as String? ?? '', province: json['province'] as String?, postalCode: json['postalCode'] as String? ?? '', country: json['country'] as String? ?? 'Sri Lanka', isDefault: json['isDefault'] as bool? ?? false);
  Map<String, dynamic> toJson() => {'label': label, 'addressLine1': addressLine1, 'addressLine2': addressLine2, 'city': city, 'province': province, 'postalCode': postalCode, 'country': country, 'isDefault': isDefault};
}
