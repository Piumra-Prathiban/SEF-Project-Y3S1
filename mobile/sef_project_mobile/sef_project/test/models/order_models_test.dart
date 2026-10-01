import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/order_models.dart';

const Map<String, dynamic> _baseOrderJson = {
  'id': 'order-1',
  'orderNumber': 'ORD-TEST-1',
  'status': 0,
  'placedAt': '2026-09-26T00:00:00Z',
  'subtotal': 5000,
  'discountTotal': 1000,
  'taxAmount': 0,
  'shippingFee': 0,
  'total': 4000,
  'currency': 'LKR',
  'items': <Map<String, dynamic>>[],
  'payments': <Map<String, dynamic>>[],
  'shipments': <Map<String, dynamic>>[],
  'statusHistory': <Map<String, dynamic>>[],
};

void main() {
  group('Order.fromJson', () {
    test('reads the applied coupon code when present', () {
      final json = {..._baseOrderJson, 'couponCode': 'SUMMER20'};

      final order = Order.fromJson(json);

      expect(order.couponCode, 'SUMMER20');
      expect(order.subtotal, 5000);
      expect(order.discountTotal, 1000);
      expect(order.total, 4000);
    });

    test('tolerates a missing couponCode (older responses / no coupon used)', () {
      final order = Order.fromJson(_baseOrderJson);

      expect(order.couponCode, isNull);
    });
  });

  group('Order.hasDiscount', () {
    test('is true when discountTotal is greater than zero', () {
      final order = Order.fromJson({..._baseOrderJson, 'discountTotal': 500});
      expect(order.hasDiscount, isTrue);
    });

    test('is false when discountTotal is zero', () {
      final order = Order.fromJson({..._baseOrderJson, 'discountTotal': 0});
      expect(order.hasDiscount, isFalse);
    });
  });
}
