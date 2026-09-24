import 'package:flutter/material.dart';

import '../models/order_models.dart';
import '../services/api_client.dart';
import '../services/order_service.dart';
import '../utils/formatters.dart';
import '../widgets/error_view.dart';
import '../widgets/status_badge.dart';
import 'order_detail_screen.dart';

class OrdersScreen extends StatefulWidget {
  const OrdersScreen({
    super.key,
    required this.orderService,
    required this.token,
    required this.onSignOut,
  });

  final OrderService orderService;
  final String token;
  final VoidCallback onSignOut;

  @override
  State<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends State<OrdersScreen> {
  final List<OrderSummary> _orders = [];

  bool _loading = true;
  bool _loadingMore = false;
  String? _error;
  int _page = 1;
  int _pageSize = 20;
  int _totalCount = 0;

  bool get _hasMore => _orders.length < _totalCount;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool append = false}) async {
    setState(() {
      if (append) {
        _loadingMore = true;
      } else {
        _loading = true;
      }

      _error = null;
    });

    try {
      final result = await widget.orderService.getOrders(
        token: widget.token,
        page: append ? _page + 1 : 1,
        pageSize: _pageSize,
      );

      if (!mounted) {
        return;
      }

      setState(() {
        if (append) {
          _orders.addAll(result.items);
        } else {
          _orders
            ..clear()
            ..addAll(result.items);
        }

        _page = result.page;
        _pageSize = result.pageSize;
        _totalCount = result.totalCount;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      if (error.status == 401) {
        widget.onSignOut();
        return;
      }

      setState(() => _error = error.message);
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Something went wrong. Please try again.');
      }
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
          _loadingMore = false;
        });
      }
    }
  }

  void _openOrder(OrderSummary order) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => OrderDetailScreen(
          orderService: widget.orderService,
          token: widget.token,
          orderId: order.id,
          onSignOut: widget.onSignOut,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Orders'),
        actions: [
          IconButton(
            onPressed: widget.onSignOut,
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
          ),
        ],
      ),
      body: _buildBody(context),
    );
  }

  Widget _buildBody(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return ErrorView(message: _error!, onRetry: _load);
    }

    if (_orders.isEmpty) {
      return const Center(child: Text('You have no orders yet.'));
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: _orders.length + (_hasMore ? 1 : 0),
        separatorBuilder: (_, _) => const Divider(height: 1),
        itemBuilder: (context, index) {
          if (index >= _orders.length) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: _loadingMore
                  ? const Center(child: CircularProgressIndicator())
                  : FilledButton.tonal(
                      onPressed: () => _load(append: true),
                      child: Text('Load more (${_orders.length} of $_totalCount)'),
                    ),
            );
          }

          final order = _orders[index];

          return ListTile(
            onTap: () => _openOrder(order),
            title: Text(
              order.orderNumber,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            subtitle: Text(formatDate(order.placedAt)),
            trailing: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                StatusBadge(status: order.status),
                const SizedBox(height: 6),
                Text(formatCurrency(order.total, order.currency)),
              ],
            ),
          );
        },
      ),
    );
  }
}
