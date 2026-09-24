import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sef_project/services/api_client.dart';
import 'package:sef_project/services/order_service.dart';

const String testToken = 'test-token';
const String testOrderId = '11111111-1111-1111-1111-111111111111';

http.Response okJson(Object body) => http.Response(
      jsonEncode(body),
      200,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

http.Response errorJson(int status, String detail) => http.Response(
      jsonEncode({'detail': detail}),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

ApiClient apiClientWith(
  Future<http.Response> Function(http.Request request) handler,
) =>
    ApiClient(
      httpClient: MockClient(handler),
      baseUrl: 'http://localhost:5193/api',
    );

OrderService orderServiceWith(
  Future<http.Response> Function(http.Request request) handler,
) =>
    OrderService(apiClientWith(handler));

Map<String, dynamic> loginJson() => {
      'token': testToken,
      'expiresAt': '2026-09-24T12:00:00Z',
      'user': {
        'id': 1,
        'email': 'customer@example.com',
        'firstName': 'Asha',
        'lastName': 'Perera',
        'role': 'Customer',
      },
    };

Map<String, dynamic> orderSummaryJson({
  String id = testOrderId,
  String orderNumber = 'ORD-1001',
  int status = 0,
  String placedAt = '2026-09-20T10:00:00Z',
  double total = 2505.5,
  String currency = 'LKR',
}) =>
    {
      'id': id,
      'orderNumber': orderNumber,
      'status': status,
      'placedAt': placedAt,
      'total': total,
      'currency': currency,
    };

Map<String, dynamic> orderListJson({
  List<Map<String, dynamic>>? items,
  int totalCount = 2,
  int page = 1,
  int pageSize = 20,
}) =>
    {
      'items': items ??
          [
            orderSummaryJson(),
            orderSummaryJson(
              id: '22222222-2222-2222-2222-222222222222',
              orderNumber: 'ORD-1002',
              status: 4,
              total: 1800,
            ),
          ],
      'totalCount': totalCount,
      'page': page,
      'pageSize': pageSize,
    };

Map<String, dynamic> paymentJson({
  String id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  double amount = 2505.5,
  int method = 0,
  int status = 1,
  String? transactionReference = 'MOCK-1234',
  String? paidAt = '2026-09-20T10:05:00Z',
}) =>
    {
      'id': id,
      'amount': amount,
      'method': method,
      'status': status,
      'transactionReference': transactionReference,
      'paidAt': paidAt,
    };

Map<String, dynamic> shipmentJson({
  String id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  int status = 1,
  String? trackingNumber = 'TRACK-99',
  String? carrier = 'LankaExpress',
  String? shippedAt = '2026-09-21T08:00:00Z',
  String? deliveredAt,
}) =>
    {
      'id': id,
      'status': status,
      'trackingNumber': trackingNumber,
      'carrier': carrier,
      'shippedAt': shippedAt,
      'deliveredAt': deliveredAt,
    };

Map<String, dynamic> orderDetailJson({
  int status = 4,
  List<Map<String, dynamic>>? payments,
  List<Map<String, dynamic>>? shipments,
  List<Map<String, dynamic>>? statusHistory,
}) =>
    {
      'id': testOrderId,
      'orderNumber': 'ORD-1001',
      'status': status,
      'placedAt': '2026-09-20T10:00:00Z',
      'subtotal': 2300.0,
      'discountTotal': 0.0,
      'taxAmount': 105.0,
      'shippingFee': 100.5,
      'total': 2505.5,
      'currency': 'LKR',
      'items': [
        {
          'id': '99999999-9999-9999-9999-999999999999',
          'productVariantId': '88888888-8888-8888-8888-888888888888',
          'sku': 'DEN-001',
          'name': 'Slim Fit Denim Jacket',
          'quantity': 2,
          'unitPrice': 1150.0,
          'lineTotal': 2300.0,
        },
      ],
      'deliveryAddress': {
        'fullName': 'Asha Perera',
        'line1': '12 Galle Road',
        'line2': null,
        'city': 'Colombo',
        'province': null,
        'postalCode': '00300',
        'country': 'Sri Lanka',
        'phone': null,
      },
      'payments': payments ?? [paymentJson()],
      'shipments': shipments ?? [shipmentJson()],
      'statusHistory': statusHistory ??
          [
            {
              'id': 'cccccccc-cccc-cccc-cccc-cccccccccccc',
              'status': 0,
              'changedAt': '2026-09-20T10:00:00Z',
              'note': 'Order placed',
            },
            {
              'id': 'dddddddd-dddd-dddd-dddd-dddddddddddd',
              'status': 4,
              'changedAt': '2026-09-22T18:30:00Z',
              'note': null,
            },
          ],
    };
