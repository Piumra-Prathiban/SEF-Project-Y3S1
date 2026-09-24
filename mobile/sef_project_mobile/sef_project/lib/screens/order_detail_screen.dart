import 'package:flutter/material.dart';

import '../models/order_enums.dart';
import '../models/order_models.dart';
import '../services/api_client.dart';
import '../services/order_service.dart';
import '../utils/formatters.dart';
import '../widgets/error_view.dart';
import '../widgets/shipment_progress.dart';
import '../widgets/status_badge.dart';

class OrderDetailScreen extends StatefulWidget {
  const OrderDetailScreen({
    super.key,
    required this.orderService,
    required this.token,
    required this.orderId,
    required this.onSignOut,
  });

  final OrderService orderService;
  final String token;
  final String orderId;
  final VoidCallback onSignOut;

  @override
  State<OrderDetailScreen> createState() => _OrderDetailScreenState();
}

class _OrderDetailScreenState extends State<OrderDetailScreen> {
  Order? _order;
  bool _loading = true;
  bool _notFound = false;
  bool _cancelling = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _notFound = false;
      _error = null;
    });

    try {
      final order = await widget.orderService.getOrderById(
        token: widget.token,
        orderId: widget.orderId,
      );

      if (!mounted) {
        return;
      }

      setState(() => _order = order);
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      if (error.status == 401) {
        widget.onSignOut();
        return;
      }

      if (error.status == 404) {
        setState(() => _notFound = true);
        return;
      }

      setState(() => _error = error.message);
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Something went wrong. Please try again.');
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  // Same approach as the React app: no cancellable-status rules are encoded
  // here - only the two states where cancellation can never apply again are
  // hidden, and the backend rejects anything else with an explanation.
  bool get _canCancel =>
      _order != null &&
      _order!.status != orderStatusCancelled &&
      _order!.status != orderStatusRefunded;

  Future<bool> _confirmCancellation() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this order?'),
        content: const Text(
          'This cannot be undone. Reserved stock is released, unshipped '
          'shipments are cancelled and completed payments are refunded.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Keep order'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Yes, cancel order'),
          ),
        ],
      ),
    );

    return confirmed ?? false;
  }

  void _showMessage(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Theme.of(context).colorScheme.error : null,
      ),
    );
  }

  Future<void> _cancelOrder() async {
    final confirmed = await _confirmCancellation();

    if (!confirmed || !mounted) {
      return;
    }

    setState(() {
      _cancelling = true;
      _error = null;
    });

    try {
      final order = await widget.orderService.cancelOrder(
        token: widget.token,
        orderId: widget.orderId,
      );

      if (!mounted) {
        return;
      }

      setState(() => _order = order);
      _showMessage('Order cancelled.');
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      if (error.status == 401) {
        widget.onSignOut();
        return;
      }

      _showMessage(error.message, isError: true);
    } catch (_) {
      if (mounted) {
        _showMessage('The order could not be cancelled.', isError: true);
      }
    } finally {
      if (mounted) {
        setState(() => _cancelling = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_order?.orderNumber ?? 'Order'),
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

    if (_notFound) {
      return const Center(child: Text('Order not found'));
    }

    if (_error != null) {
      return ErrorView(message: _error!, onRetry: _load);
    }

    final order = _order;

    if (order == null) {
      return const SizedBox.shrink();
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                order.orderNumber,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
            StatusBadge(status: order.status),
          ],
        ),
        const SizedBox(height: 4),
        Text('Placed ${formatDateTime(order.placedAt)}'),
        _Section(
          title: 'Cancel order',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                _canCancel
                    ? 'Cancellation cannot be undone. Reserved stock is '
                        'released, unshipped shipments are cancelled and '
                        'completed payments are refunded.'
                    : 'This order can no longer be cancelled.',
              ),
              if (_canCancel) ...[
                const SizedBox(height: 12),
                FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: Theme.of(context).colorScheme.error,
                    foregroundColor: Theme.of(context).colorScheme.onError,
                  ),
                  onPressed: _cancelling ? null : _cancelOrder,
                  child: Text(_cancelling ? 'Cancelling…' : 'Cancel order'),
                ),
              ],
            ],
          ),
        ),
        _Section(
          title: 'Items',
          child: Column(
            children: order.items
                .map((item) => _ItemRow(item: item, currency: order.currency))
                .toList(),
          ),
        ),
        _Section(
          title: 'Totals',
          child: Column(
            children: [
              _DetailRow(
                label: 'Subtotal',
                value: formatCurrency(order.subtotal, order.currency),
              ),
              _DetailRow(
                label: 'Discount',
                value: formatCurrency(order.discountTotal, order.currency),
              ),
              _DetailRow(
                label: 'Tax',
                value: formatCurrency(order.taxAmount, order.currency),
              ),
              _DetailRow(
                label: 'Shipping',
                value: formatCurrency(order.shippingFee, order.currency),
              ),
              _DetailRow(
                label: 'Total',
                value: formatCurrency(order.total, order.currency),
                emphasised: true,
              ),
            ],
          ),
        ),
        if (order.deliveryAddress != null)
          _Section(
            title: 'Delivery address',
            child: _AddressBlock(address: order.deliveryAddress!),
          ),
        _Section(
          title: 'Payments',
          child: order.payments.isEmpty
              ? const Text('No payments recorded.')
              : Column(
                  children: order.payments
                      .map(
                        (payment) => _PaymentRow(
                          payment: payment,
                          currency: order.currency,
                        ),
                      )
                      .toList(),
                ),
        ),
        _Section(
          title: 'Shipments',
          child: order.shipments.isEmpty
              ? const Text('No shipments recorded.')
              : Column(
                  children: order.shipments
                      .map((shipment) => _ShipmentBlock(shipment: shipment))
                      .toList(),
                ),
        ),
        _Section(
          title: 'Status history',
          child: order.statusHistory.isEmpty
              ? const Text('No status changes recorded.')
              : Column(
                  children: order.statusHistory
                      .map((entry) => _HistoryRow(entry: entry))
                      .toList(),
                ),
        ),
      ],
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          child,
        ],
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({
    required this.label,
    required this.value,
    this.emphasised = false,
  });

  final String label;
  final String value;
  final bool emphasised;

  @override
  Widget build(BuildContext context) {
    final style = emphasised ? const TextStyle(fontWeight: FontWeight.w700) : null;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: style),
          Text(value, style: style),
        ],
      ),
    );
  }
}

class _ItemRow extends StatelessWidget {
  const _ItemRow({required this.item, required this.currency});

  final OrderItem item;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(item.name, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 2),
          Text('${item.sku} · Qty ${item.quantity}'),
          const SizedBox(height: 2),
          Text(
            '${formatCurrency(item.unitPrice, currency)} each · '
            '${formatCurrency(item.lineTotal, currency)}',
          ),
        ],
      ),
    );
  }
}

class _AddressBlock extends StatelessWidget {
  const _AddressBlock({required this.address});

  final OrderAddress address;

  @override
  Widget build(BuildContext context) {
    final lines = [
      address.fullName,
      [address.line1, address.line2]
          .whereType<String>()
          .where((line) => line.isNotEmpty)
          .join(', '),
      [address.city, address.province]
          .whereType<String>()
          .where((line) => line.isNotEmpty)
          .join(', '),
      '${address.postalCode} ${address.country}',
      if (address.phone != null && address.phone!.isNotEmpty) address.phone!,
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: lines.map((line) => Text(line)).toList(),
    );
  }
}

class _PaymentRow extends StatelessWidget {
  const _PaymentRow({required this.payment, required this.currency});

  final Payment payment;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final method = paymentMethodNames[payment.method] ?? payment.method.toString();

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              StatusBadge(status: payment.status, kind: StatusKind.payment),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  '$method · ${formatCurrency(payment.amount, currency)}',
                ),
              ),
            ],
          ),
          if (payment.paidAt != null)
            Text('Paid ${formatDateTime(payment.paidAt!)}'),
          if (payment.transactionReference != null)
            Text('Reference ${payment.transactionReference}'),
        ],
      ),
    );
  }
}

class _ShipmentBlock extends StatelessWidget {
  const _ShipmentBlock({required this.shipment});

  final Shipment shipment;

  @override
  Widget build(BuildContext context) {
    final carrierAndTracking = [
      if (shipment.carrier != null && shipment.carrier!.isNotEmpty) shipment.carrier!,
      if (shipment.trackingNumber != null && shipment.trackingNumber!.isNotEmpty)
        shipment.trackingNumber!,
    ].join(' · ');

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          StatusBadge(status: shipment.status, kind: StatusKind.shipment),
          const SizedBox(height: 6),
          ShipmentProgress(status: shipment.status),
          if (carrierAndTracking.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(carrierAndTracking),
          ],
          if (shipment.shippedAt != null)
            Text('Shipped ${formatDateTime(shipment.shippedAt!)}'),
          if (shipment.deliveredAt != null)
            Text('Delivered ${formatDateTime(shipment.deliveredAt!)}'),
        ],
      ),
    );
  }
}

class _HistoryRow extends StatelessWidget {
  const _HistoryRow({required this.entry});

  final StatusHistoryEntry entry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          StatusBadge(status: entry.status),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(formatDateTime(entry.changedAt)),
                if (entry.note != null && entry.note!.isNotEmpty)
                  Text(entry.note!),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
