import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';

class CartScreen extends StatefulWidget {
  const CartScreen({super.key});

  @override
  State<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends State<CartScreen> {
  bool _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _loaded = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        StoreScope.of(context).loadCart().catchError((_) {});
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    final cart = store.cart;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Cart'),
        actions: [
          if (cart.items.isNotEmpty)
            TextButton(
              onPressed: () => _confirmClear(context, store),
              child: const Text('Clear'),
            ),
        ],
      ),
      body: AsyncPanel(
        loading: store.loading && cart.items.isEmpty,
        error: store.error,
        empty: cart.items.isEmpty,
        emptyTitle: 'Your cart is empty',
        emptyMessage: 'Choose an available product variant to get started.',
        onRetry: () => store.loadCart(),
        child: Column(
          children: [
            Expanded(
              child: RefreshIndicator(
                onRefresh: store.loadCart,
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: cart.items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 10),
                  itemBuilder: (context, index) =>
                      _CartItemCard(item: cart.items[index]),
                ),
              ),
            ),
            _CartSummary(cart: cart),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmClear(
      BuildContext context, CustomerStore store) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Clear cart?'),
        content: const Text('This will remove every item from your cart.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel')),
          FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Clear cart')),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await runAction(context, store.clearCart, success: 'Cart cleared.');
    }
  }
}

class _CartItemCard extends StatelessWidget {
  const _CartItemCard({required this.item});

  final CartItem item;

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const CircleAvatar(child: Icon(Icons.shopping_bag_outlined)),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(item.productName,
                          style: Theme.of(context).textTheme.titleMedium),
                      Text(item.variantName),
                      Text('SKU: ${item.sku}',
                          style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Remove item',
                  onPressed: () => runAction(
                    context,
                    () => store.removeCartItem(item.id),
                    success: 'Item removed.',
                  ),
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                IconButton(
                  tooltip: 'Decrease quantity',
                  onPressed: item.quantity > 1
                      ? () => runAction(
                            context,
                            () => store.updateCart(item.id, item.quantity - 1),
                          )
                      : null,
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                Text('${item.quantity}',
                    style: Theme.of(context).textTheme.titleMedium),
                IconButton(
                  tooltip: 'Increase quantity',
                  onPressed: item.quantity < item.availableQuantity
                      ? () => runAction(
                            context,
                            () => store.updateCart(item.id, item.quantity + 1),
                          )
                      : null,
                  icon: const Icon(Icons.add_circle_outline),
                ),
                const Spacer(),
                Text(
                  money(item.lineTotal),
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            if (!item.hasSufficientStock)
              Text(
                'Only ${item.availableQuantity} available. Reduce the quantity.',
                style: TextStyle(color: Colors.red.shade700),
              ),
          ],
        ),
      ),
    );
  }
}

class _CartSummary extends StatelessWidget {
  const _CartSummary({required this.cart});

  final Cart cart;

  @override
  Widget build(BuildContext context) => SafeArea(
        top: false,
        child: Material(
          elevation: 8,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('${cart.totalQuantity} items'),
                    Text('Subtotal ${money(cart.subtotal, cart.currency)}'),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('Total',
                        style: Theme.of(context).textTheme.titleLarge),
                    Text(
                      money(cart.total, cart.currency),
                      style: Theme.of(context)
                          .textTheme
                          .titleLarge
                          ?.copyWith(fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    onPressed: cart.items.every((item) =>
                            item.hasSufficientStock && item.quantity > 0)
                        ? () => showDialog<void>(
                              context: context,
                              builder: (context) => AlertDialog(
                                title: const Text('Ready for checkout'),
                                content: const Text(
                                  'Checkout and order creation continue in the Orders experience.',
                                ),
                                actions: [
                                  FilledButton(
                                    onPressed: () => Navigator.pop(context),
                                    child: const Text('OK'),
                                  ),
                                ],
                              ),
                            )
                        : null,
                    icon: const Icon(Icons.arrow_forward),
                    label: const Text('Continue to checkout'),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
}
