/// DTO models mirroring the backend `DTOs/Orders/*.cs` shapes (camelCase on the
/// wire), the same contracts the React client consumes.
class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.placedAt,
    required this.total,
    required this.currency,
  });

  final String id;
  final String orderNumber;
  final int status;
  final DateTime placedAt;
  final double total;
  final String currency;

  factory OrderSummary.fromJson(Map<String, dynamic> json) => OrderSummary(
        id: json['id'] as String,
        orderNumber: json['orderNumber'] as String,
        status: (json['status'] as num).toInt(),
        placedAt: DateTime.parse(json['placedAt'] as String),
        total: (json['total'] as num).toDouble(),
        currency: json['currency'] as String,
      );
}

class OrderList {
  const OrderList({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });

  final List<OrderSummary> items;
  final int totalCount;
  final int page;
  final int pageSize;

  factory OrderList.fromJson(Map<String, dynamic> json) => OrderList(
        items: (json['items'] as List<dynamic>)
            .map((item) => OrderSummary.fromJson(item as Map<String, dynamic>))
            .toList(),
        totalCount: (json['totalCount'] as num).toInt(),
        page: (json['page'] as num).toInt(),
        pageSize: (json['pageSize'] as num).toInt(),
      );
}

class OrderItem {
  const OrderItem({
    required this.id,
    required this.productVariantId,
    required this.sku,
    required this.name,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });

  final String id;
  final String productVariantId;
  final String sku;
  final String name;
  final int quantity;
  final double unitPrice;
  final double lineTotal;

  factory OrderItem.fromJson(Map<String, dynamic> json) => OrderItem(
        id: json['id'] as String,
        productVariantId: json['productVariantId'] as String,
        sku: json['sku'] as String,
        name: json['name'] as String,
        quantity: (json['quantity'] as num).toInt(),
        unitPrice: (json['unitPrice'] as num).toDouble(),
        lineTotal: (json['lineTotal'] as num).toDouble(),
      );
}

class OrderAddress {
  const OrderAddress({
    required this.fullName,
    required this.line1,
    this.line2,
    required this.city,
    this.province,
    required this.postalCode,
    required this.country,
    this.phone,
  });

  final String fullName;
  final String line1;
  final String? line2;
  final String city;
  final String? province;
  final String postalCode;
  final String country;
  final String? phone;

  factory OrderAddress.fromJson(Map<String, dynamic> json) => OrderAddress(
        fullName: json['fullName'] as String,
        line1: json['line1'] as String,
        line2: json['line2'] as String?,
        city: json['city'] as String,
        province: json['province'] as String?,
        postalCode: json['postalCode'] as String,
        country: json['country'] as String,
        phone: json['phone'] as String?,
      );
}

class Payment {
  const Payment({
    required this.id,
    required this.amount,
    required this.method,
    required this.status,
    this.transactionReference,
    this.paidAt,
  });

  final String id;
  final double amount;
  final int method;
  final int status;
  final String? transactionReference;
  final DateTime? paidAt;

  factory Payment.fromJson(Map<String, dynamic> json) => Payment(
        id: json['id'] as String,
        amount: (json['amount'] as num).toDouble(),
        method: (json['method'] as num).toInt(),
        status: (json['status'] as num).toInt(),
        transactionReference: json['transactionReference'] as String?,
        paidAt: json['paidAt'] == null
            ? null
            : DateTime.parse(json['paidAt'] as String),
      );
}

class Shipment {
  const Shipment({
    required this.id,
    required this.status,
    this.trackingNumber,
    this.carrier,
    this.shippedAt,
    this.deliveredAt,
  });

  final String id;
  final int status;
  final String? trackingNumber;
  final String? carrier;
  final DateTime? shippedAt;
  final DateTime? deliveredAt;

  factory Shipment.fromJson(Map<String, dynamic> json) => Shipment(
        id: json['id'] as String,
        status: (json['status'] as num).toInt(),
        trackingNumber: json['trackingNumber'] as String?,
        carrier: json['carrier'] as String?,
        shippedAt: json['shippedAt'] == null
            ? null
            : DateTime.parse(json['shippedAt'] as String),
        deliveredAt: json['deliveredAt'] == null
            ? null
            : DateTime.parse(json['deliveredAt'] as String),
      );
}

class StatusHistoryEntry {
  const StatusHistoryEntry({
    required this.id,
    required this.status,
    required this.changedAt,
    this.note,
  });

  final String id;
  final int status;
  final DateTime changedAt;
  final String? note;

  factory StatusHistoryEntry.fromJson(Map<String, dynamic> json) =>
      StatusHistoryEntry(
        id: json['id'] as String,
        status: (json['status'] as num).toInt(),
        changedAt: DateTime.parse(json['changedAt'] as String),
        note: json['note'] as String?,
      );
}

class Order {
  const Order({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.placedAt,
    required this.subtotal,
    required this.discountTotal,
    required this.taxAmount,
    required this.shippingFee,
    required this.total,
    required this.currency,
    required this.items,
    this.deliveryAddress,
    required this.payments,
    required this.shipments,
    required this.statusHistory,
  });

  final String id;
  final String orderNumber;
  final int status;
  final DateTime placedAt;
  final double subtotal;
  final double discountTotal;
  final double taxAmount;
  final double shippingFee;
  final double total;
  final String currency;
  final List<OrderItem> items;
  final OrderAddress? deliveryAddress;
  final List<Payment> payments;
  final List<Shipment> shipments;
  final List<StatusHistoryEntry> statusHistory;

  factory Order.fromJson(Map<String, dynamic> json) => Order(
        id: json['id'] as String,
        orderNumber: json['orderNumber'] as String,
        status: (json['status'] as num).toInt(),
        placedAt: DateTime.parse(json['placedAt'] as String),
        subtotal: (json['subtotal'] as num).toDouble(),
        discountTotal: (json['discountTotal'] as num).toDouble(),
        taxAmount: (json['taxAmount'] as num).toDouble(),
        shippingFee: (json['shippingFee'] as num).toDouble(),
        total: (json['total'] as num).toDouble(),
        currency: json['currency'] as String,
        items: (json['items'] as List<dynamic>)
            .map((item) => OrderItem.fromJson(item as Map<String, dynamic>))
            .toList(),
        deliveryAddress: json['deliveryAddress'] == null
            ? null
            : OrderAddress.fromJson(
                json['deliveryAddress'] as Map<String, dynamic>,
              ),
        payments: (json['payments'] as List<dynamic>)
            .map((payment) => Payment.fromJson(payment as Map<String, dynamic>))
            .toList(),
        shipments: (json['shipments'] as List<dynamic>)
            .map(
              (shipment) => Shipment.fromJson(shipment as Map<String, dynamic>),
            )
            .toList(),
        statusHistory: (json['statusHistory'] as List<dynamic>)
            .map(
              (entry) =>
                  StatusHistoryEntry.fromJson(entry as Map<String, dynamic>),
            )
            .toList(),
      );
}
