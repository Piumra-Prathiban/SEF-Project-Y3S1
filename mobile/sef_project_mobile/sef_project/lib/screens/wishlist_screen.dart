import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';
import 'product_detail_screen.dart';

class WishlistScreen extends StatefulWidget {
  const WishlistScreen({super.key});

  @override
  State<WishlistScreen> createState() => _WishlistScreenState();
}

class _WishlistScreenState extends State<WishlistScreen> {
  bool _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _loaded = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        StoreScope.of(context).loadWishlist().catchError((_) {});
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Wishlist')),
      body: AsyncPanel(
        loading: store.loading && store.wishlist.isEmpty,
        error: store.error,
        empty: store.wishlist.isEmpty,
        emptyTitle: 'Your wishlist is empty',
        emptyMessage: 'Save products while browsing and they will appear here.',
        onRetry: () => store.loadWishlist(),
        child: RefreshIndicator(
          onRefresh: store.loadWishlist,
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: store.wishlist.length,
            separatorBuilder: (_, _) => const SizedBox(height: 10),
            itemBuilder: (context, index) => _WishlistCard(
              item: store.wishlist[index],
            ),
          ),
        ),
      ),
    );
  }
}

class _WishlistCard extends StatelessWidget {
  const _WishlistCard({required this.item});

  final WishlistItem item;

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            CircleAvatar(
              radius: 28,
              child: const Icon(Icons.favorite_outline),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.productName,
                      style: Theme.of(context).textTheme.titleMedium),
                  if (item.description?.isNotEmpty == true)
                    Text(
                      item.description!,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  const SizedBox(height: 6),
                  Text(
                    item.minimumPrice == null
                        ? 'Price unavailable'
                        : 'From ${money(item.minimumPrice!)}',
                  ),
                  Text(
                    item.isAvailable ? 'Available' : 'Currently unavailable',
                    style: TextStyle(
                      color: item.isAvailable
                          ? Colors.green.shade700
                          : Colors.red.shade700,
                    ),
                  ),
                ],
              ),
            ),
            Column(
              children: [
                IconButton(
                  tooltip: 'Choose variant and add to cart',
                  onPressed: item.isAvailable
                      ? () => _openProduct(context, store)
                      : null,
                  icon: const Icon(Icons.add_shopping_cart),
                ),
                IconButton(
                  tooltip: 'Remove from wishlist',
                  onPressed: () => runAction(
                    context,
                    () => store.removeFromWishlist(item.productId),
                    success: 'Removed from wishlist.',
                  ),
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _openProduct(
      BuildContext context, CustomerStore store) async {
    try {
      final product = await store.productFor(item);
      if (!context.mounted) return;
      if (product == null) {
        showMessage(context, 'Product details are not currently available.');
        return;
      }
      await Navigator.push<void>(
        context,
        MaterialPageRoute(
          builder: (_) => StoreScope(
            store: store,
            child: ProductDetailScreen(product: product),
          ),
        ),
      );
    } catch (error) {
      if (context.mounted) showMessage(context, error.toString());
    }
  }
}
