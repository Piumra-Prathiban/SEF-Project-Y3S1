import '../models/order_models.dart';
import 'api_client.dart';

/// Orders & Fulfilment endpoints used by the customer app. The token is passed
/// explicitly, matching the React client's approach. Customer ownership is
/// enforced by the backend (it filters to the caller's own orders), so no
/// customer id is ever sent from the client.
class OrderService {
  OrderService(this._client);

  final ApiClient _client;

  Future<OrderList> getOrders({
    required String token,
    int page = 1,
    int pageSize = 20,
  }) async {
    final data = await _client.get(
      '/Orders?page=$page&pageSize=$pageSize',
      token: token,
    );

    return OrderList.fromJson(data as Map<String, dynamic>);
  }

  Future<Order> getOrderById({
    required String token,
    required String orderId,
  }) async {
    final data = await _client.get('/Orders/$orderId', token: token);

    return Order.fromJson(data as Map<String, dynamic>);
  }

  /// Cancels an order the caller owns. The backend owns the rules (which
  /// statuses may be cancelled, shipment checks, inventory release, refunds);
  /// a rejected cancellation surfaces as an ApiException. The response is the
  /// full updated order, so it refreshes status, payments and shipments.
  Future<Order> cancelOrder({
    required String token,
    required String orderId,
  }) async {
    final data = await _client.post(
      '/Orders/$orderId/cancel',
      token: token,
      body: const <String, dynamic>{},
    );

    return Order.fromJson(data as Map<String, dynamic>);
  }
}
