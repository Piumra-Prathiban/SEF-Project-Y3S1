import 'package:flutter/material.dart';

import '../models/order_enums.dart';
import '../models/order_models.dart';
import '../services/customer_api.dart';
import '../state/customer_store.dart';
import '../utils/formatters.dart';
import '../widgets/shipment_progress.dart';
import '../widgets/status_badge.dart';

class CustomerOrdersScreen extends StatefulWidget {
  const CustomerOrdersScreen({super.key});

  @override
  State<CustomerOrdersScreen> createState() => _CustomerOrdersScreenState();
}

class _CustomerOrdersScreenState extends State<CustomerOrdersScreen> {
  OrderList? _orders;
  bool _loading = true;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_orders == null && _loading) _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await StoreScope.of(context).api.orders();
      if (mounted) setState(() => _orders = result);
    } catch (exception) {
      if (!mounted) return;
      if (exception is ApiException && exception.statusCode == 401) {
        await StoreScope.of(context).logout();
      } else {
        setState(() => _error = exception.toString());
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('My orders')),
    body: _loading
        ? const Center(child: CircularProgressIndicator())
        : _error != null
            ? Center(
                child: FilledButton.tonal(
                  onPressed: _load,
                  child: Text('Try again · $_error'),
                ),
              )
            : _orders == null || _orders!.items.isEmpty
                ? const Center(child: Text('You have no orders yet.'))
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _orders!.items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 10),
                      itemBuilder: (context, index) {
                        final order = _orders!.items[index];
                        return Card(
                          child: ListTile(
                            contentPadding: const EdgeInsets.all(16),
                            title: Text(order.orderNumber),
                            subtitle: Padding(
                              padding: const EdgeInsets.only(top: 6),
                              child: Text('${formatDate(order.placedAt)} · ${formatCurrency(order.total, order.currency)}'),
                            ),
                            trailing: StatusBadge(status: order.status),
                            onTap: () async {
                              await Navigator.of(context).push(
                                MaterialPageRoute<void>(
                                  builder: (_) => CustomerOrderDetailScreen(orderId: order.id),
                                ),
                              );
                              if (mounted) _load();
                            },
                          ),
                        );
                      },
                    ),
                  ),
  );
}

class CustomerOrderDetailScreen extends StatefulWidget {
  const CustomerOrderDetailScreen({super.key, required this.orderId});
  final String orderId;

  @override
  State<CustomerOrderDetailScreen> createState() => _CustomerOrderDetailScreenState();
}

class _CustomerOrderDetailScreenState extends State<CustomerOrderDetailScreen> {
  Order? _order;
  List<ProductReturn> _returns = [];
  bool _loading = true;
  bool _busy = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_order == null && _loading) _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final api = StoreScope.of(context).api;
      final results = await Future.wait([
        api.order(widget.orderId),
        api.orderReturns(widget.orderId),
      ]);
      if (mounted) {
        setState(() {
          _order = results[0] as Order;
          _returns = results[1] as List<ProductReturn>;
        });
      }
    } catch (exception) {
      if (mounted) setState(() => _error = exception.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _cancelOrder() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Cancel order?'),
        content: const Text('This releases reserved stock and refunds completed payments. It cannot be undone.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep order')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Cancel order')),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() => _busy = true);
    try {
      _order = await StoreScope.of(context).api.cancelOrder(widget.orderId);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Order cancelled.')));
    } catch (exception) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(exception.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _requestReturn() async {
    final order = _order!;
    final request = await showModalBottomSheet<_ReturnDraft>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ReturnRequestSheet(order: order, existingReturns: _returns),
    );
    if (request == null || !mounted) return;
    setState(() => _busy = true);
    try {
      await StoreScope.of(context).api.createReturn(
        order.id,
        reason: request.reason,
        items: request.quantities.entries
            .where((entry) => entry.value > 0)
            .map((entry) => {'orderItemId': entry.key, 'quantity': entry.value})
            .toList(),
        note: request.note,
      );
      await _load();
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Return request submitted.')));
    } catch (exception) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(exception.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _cancelReturn(ProductReturn itemReturn) async {
    setState(() => _busy = true);
    try {
      await StoreScope.of(context).api.cancelReturn(itemReturn.id);
      await _load();
    } catch (exception) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(exception.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_error != null || _order == null) {
      return Scaffold(appBar: AppBar(), body: Center(child: FilledButton.tonal(onPressed: _load, child: Text(_error ?? 'Try again'))));
    }
    final order = _order!;
    final canCancel = order.status != orderStatusCancelled && order.status != orderStatusRefunded && order.status != orderStatusCompleted;
    return Scaffold(
      appBar: AppBar(title: Text(order.orderNumber)),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [StatusBadge(status: order.status), Text(formatDateTime(order.placedAt))]),
            const SizedBox(height: 20),
            Text('Items', style: Theme.of(context).textTheme.titleLarge),
            ...order.items.map((item) => ListTile(contentPadding: EdgeInsets.zero, title: Text(item.name), subtitle: Text('${item.sku} · ${item.quantity} × ${formatCurrency(item.unitPrice, order.currency)}'), trailing: Text(formatCurrency(item.lineTotal, order.currency)))),
            const Divider(),
            ListTile(contentPadding: EdgeInsets.zero, title: const Text('Subtotal'), trailing: Text(formatCurrency(order.subtotal, order.currency))),
            if (order.hasDiscount)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Discount'),
                trailing: Text(
                  '- ${formatCurrency(order.discountTotal, order.currency)}',
                  style: TextStyle(color: Colors.green.shade700, fontWeight: FontWeight.w600),
                ),
              ),
            if (order.couponCode != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Icon(Icons.local_offer_outlined, size: 16, color: Colors.green.shade700),
                    const SizedBox(width: 6),
                    Text('Coupon ${order.couponCode} applied', style: TextStyle(color: Colors.green.shade700)),
                  ],
                ),
              ),
            ListTile(contentPadding: EdgeInsets.zero, title: const Text('Order total'), trailing: Text(formatCurrency(order.total, order.currency), style: const TextStyle(fontWeight: FontWeight.bold))),
            if (canCancel) FilledButton.tonalIcon(onPressed: _busy ? null : _cancelOrder, icon: const Icon(Icons.cancel_outlined), label: const Text('Cancel order')),
            const SizedBox(height: 24),
            Text('Payments', style: Theme.of(context).textTheme.titleLarge),
            if (order.payments.isEmpty) const Text('No payments recorded.') else ...order.payments.map((payment) => ListTile(contentPadding: EdgeInsets.zero, title: Text(paymentMethodNames[payment.method] ?? 'Payment'), subtitle: Text(paymentStatusNames[payment.status] ?? ''), trailing: Text(formatCurrency(payment.amount, order.currency)))),
            const SizedBox(height: 16),
            Text('Fulfilment', style: Theme.of(context).textTheme.titleLarge),
            if (order.shipments.isEmpty) const Text('Shipment has not been created yet.') else ...order.shipments.map((shipment) => _ShipmentCard(shipment: shipment)),
            const SizedBox(height: 24),
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [Text('Returns', style: Theme.of(context).textTheme.titleLarge), if (order.status == orderStatusCompleted) TextButton.icon(onPressed: _busy ? null : _requestReturn, icon: const Icon(Icons.keyboard_return), label: const Text('Request'))]),
            if (_returns.isEmpty) const Text('No returns requested.') else ..._returns.map((itemReturn) => Card(child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [Expanded(child: Text(itemReturn.returnNumber, style: const TextStyle(fontWeight: FontWeight.bold))), StatusBadge(status: itemReturn.status, kind: StatusKind.productReturn)]), const SizedBox(height: 6), Text('${returnReasonNames[itemReturn.reason]} · ${formatCurrency(itemReturn.refundAmount, itemReturn.currency)}'), ...itemReturn.items.map((item) => Text('${item.name} × ${item.quantity}')), if (itemReturn.status == returnStatusRequested) TextButton(onPressed: _busy ? null : () => _cancelReturn(itemReturn), child: const Text('Cancel request'))])))),
          ],
        ),
      ),
    );
  }
}

class _ShipmentCard extends StatelessWidget {
  const _ShipmentCard({required this.shipment});

  final Shipment shipment;

  @override
  Widget build(BuildContext context) {
    final details = <String>[
      if (shipment.carrier?.isNotEmpty == true) 'Carrier: ${shipment.carrier}',
      if (shipment.trackingNumber?.isNotEmpty == true)
        'Tracking: ${shipment.trackingNumber}',
      if (shipment.shippedAt != null)
        'Shipped: ${formatDate(shipment.shippedAt!)}',
      if (shipment.deliveredAt != null)
        'Delivered: ${formatDate(shipment.deliveredAt!)}',
    ];

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.local_shipping_outlined, size: 18),
                const SizedBox(width: 8),
                Text('Shipment', style: Theme.of(context).textTheme.titleSmall),
                const Spacer(),
                StatusBadge(
                  status: shipment.status,
                  kind: StatusKind.shipment,
                ),
              ],
            ),
            if (shipment.status != shipmentStatusCancelled) ...[
              const SizedBox(height: 12),
              ShipmentProgress(status: shipment.status),
            ],
            if (details.isNotEmpty) ...[
              const SizedBox(height: 12),
              ...details.map(
                (detail) => Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Text(
                    detail,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ReturnDraft {
  const _ReturnDraft(this.reason, this.quantities, this.note);
  final int reason;
  final Map<String, int> quantities;
  final String? note;
}

class _ReturnRequestSheet extends StatefulWidget {
  const _ReturnRequestSheet({required this.order, required this.existingReturns});
  final Order order;
  final List<ProductReturn> existingReturns;

  @override
  State<_ReturnRequestSheet> createState() => _ReturnRequestSheetState();
}

class _ReturnRequestSheetState extends State<_ReturnRequestSheet> {
  int reason = 0;
  final quantities = <String, int>{};
  final note = TextEditingController();

  @override
  void dispose() {
    note.dispose();
    super.dispose();
  }

  int remaining(OrderItem item) {
    final held = widget.existingReturns
        .where((itemReturn) => const [0, 1, 3, 4].contains(itemReturn.status))
        .expand((itemReturn) => itemReturn.items)
        .where((returnItem) => returnItem.orderItemId == item.id)
        .fold<int>(0, (sum, returnItem) => sum + returnItem.quantity);
    return (item.quantity - held).clamp(0, item.quantity);
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.viewInsetsOf(context).bottom + 20),
    child: SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Request a return', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 8),
          const Text('Choose the quantity of each item you are sending back.'),
          const SizedBox(height: 16),
          ...widget.order.items.map((item) {
            final available = remaining(item);
            return ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(item.name),
              subtitle: Text('$available returnable'),
              trailing: DropdownButton<int>(
                value: quantities[item.id] ?? 0,
                items: List.generate(available + 1, (value) => DropdownMenuItem(value: value, child: Text('$value'))),
                onChanged: (value) => setState(() => quantities[item.id] = value ?? 0),
              ),
            );
          }),
          DropdownButtonFormField<int>(
            initialValue: reason,
            decoration: const InputDecoration(labelText: 'Reason'),
            items: returnReasonNames.entries.map((entry) => DropdownMenuItem(value: entry.key, child: Text(entry.value))).toList(),
            onChanged: (value) => setState(() => reason = value ?? 0),
          ),
          const SizedBox(height: 12),
          TextField(controller: note, maxLength: 1000, maxLines: 3, decoration: const InputDecoration(labelText: 'Details (optional)')),
          FilledButton(
            onPressed: quantities.values.any((value) => value > 0)
                ? () => Navigator.pop(context, _ReturnDraft(reason, quantities, note.text.trim().isEmpty ? null : note.text.trim()))
                : null,
            child: const Text('Submit return request'),
          ),
        ],
      ),
    ),
  );
}
