import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import '../fixtures.dart';

void main() {
  group('OrderService', () {
    test('requests the paged order list with the token and parses it',
        () async {
      late http.Request captured;

      final service = orderServiceWith((request) async {
        captured = request;
        return okJson(orderListJson(totalCount: 45));
      });

      final result = await service.getOrders(
        token: testToken,
        page: 2,
        pageSize: 50,
      );

      expect(captured.url.path, '/api/Orders');
      expect(captured.url.queryParameters, {'page': '2', 'pageSize': '50'});
      expect(captured.headers['Authorization'], 'Bearer $testToken');
      expect(result.totalCount, 45);
      expect(result.page, 1);
      expect(result.pageSize, 20);
      expect(result.items, hasLength(2));
      expect(result.items.first.orderNumber, 'ORD-1001');
      expect(result.items.first.status, 0);
      expect(result.items.last.total, 1800);
    });

    test('requests a single order and parses the nested DTOs', () async {
      late http.Request captured;

      final service = orderServiceWith((request) async {
        captured = request;
        return okJson(orderDetailJson());
      });

      final order = await service.getOrderById(
        token: testToken,
        orderId: testOrderId,
      );

      expect(captured.url.path, '/api/Orders/$testOrderId');
      expect(captured.headers['Authorization'], 'Bearer $testToken');
      expect(order.orderNumber, 'ORD-1001');
      expect(order.status, 4);
      expect(order.total, 2505.5);
      expect(order.items.single.name, 'Slim Fit Denim Jacket');
      expect(order.items.single.quantity, 2);
      expect(order.deliveryAddress?.city, 'Colombo');
      expect(order.payments.single.transactionReference, 'MOCK-1234');
      expect(order.payments.single.method, 0);
      expect(order.shipments.single.carrier, 'LankaExpress');
      expect(order.statusHistory, hasLength(2));
      expect(order.statusHistory.first.status, 0);
      expect(order.statusHistory.last.note, isNull);
    });
  });
}
