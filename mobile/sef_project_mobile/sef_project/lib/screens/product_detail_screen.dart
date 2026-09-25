import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';
import '../widgets/product_image.dart';

class ProductDetailScreen extends StatefulWidget {
  const ProductDetailScreen({super.key, required this.product});
  final Product product;

  @override
  State<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends State<ProductDetailScreen> {
  ProductVariant? _selected;
  int _quantity = 1;

  @override
  void initState() {
    super.initState();
    for (final variant in widget.product.variants) {
      _selected ??= variant;
      if (variant.isAvailable) {
        _selected = variant;
        break;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final product = widget.product;
    final store = StoreScope.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(product.name),
        actions: [
          IconButton(
            tooltip: 'Save to wishlist',
            onPressed: () => runAction(
              context,
              () => store.addToWishlist(product.id),
              success: 'Saved to wishlist.',
            ),
            icon: const Icon(Icons.favorite_border),
          ),
        ],
      ),
      body: LayoutBuilder(builder: (context, constraints) {
        final image = SizedBox(
          height: constraints.maxWidth > 700 ? 460 : 310,
          child: ProductImage(product: product),
        );
        final details = Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                spacing: 6,
                children: product.categories
                    .map((item) => Chip(label: Text(item.name)))
                    .toList(),
              ),
              Text(product.name,
                  style: Theme.of(context).textTheme.headlineMedium),
              const SizedBox(height: 10),
              Text(product.description ?? 'No description available.'),
              const SizedBox(height: 20),
              Text(
                money(_selected?.price ?? product.minimumPrice),
                style: Theme.of(context)
                    .textTheme
                    .headlineSmall
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 18),
              Text('Available variants',
                  style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              if (product.variants.isEmpty)
                const Text('No variants are currently available.')
              else
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: product.variants.map((variant) {
                    final parts = [
                      variant.name,
                      if (variant.size != null) 'Size ${variant.size}',
                      if (variant.colour != null) variant.colour!,
                    ];
                    return ChoiceChip(
                      label: Text(parts.join(' / ')),
                      selected: _selected?.id == variant.id,
                      onSelected: variant.isAvailable
                          ? (_) => setState(() {
                                _selected = variant;
                                _quantity = 1;
                              })
                          : null,
                    );
                  }).toList(),
                ),
              const SizedBox(height: 12),
              Text(
                _selected?.isAvailable == true
                    ? '${_selected!.availableQuantity} available'
                    : 'Currently unavailable',
                style: TextStyle(
                  color: _selected?.isAvailable == true
                      ? Colors.green.shade700
                      : Colors.red.shade700,
                ),
              ),
              const SizedBox(height: 18),
              Row(children: [
                IconButton(
                  onPressed: _quantity > 1
                      ? () => setState(() => _quantity--)
                      : null,
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                Text('$_quantity',
                    style: Theme.of(context).textTheme.titleMedium),
                IconButton(
                  onPressed: _selected != null &&
                          _quantity < _selected!.availableQuantity
                      ? () => setState(() => _quantity++)
                      : null,
                  icon: const Icon(Icons.add_circle_outline),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton.icon(
                    onPressed: _selected?.isAvailable == true
                        ? () => runAction(
                              context,
                              () => store.addToCart(_selected!.id, _quantity),
                              success: 'Added to cart.',
                            )
                        : null,
                    icon: const Icon(Icons.add_shopping_cart),
                    label: const Text('Add to cart'),
                  ),
                ),
              ]),
              const SizedBox(height: 14),
              const Text(
                'Images, sizes, and colours are shown when supplied by the product API.',
                style: TextStyle(fontSize: 12, color: Colors.black54),
              ),
            ],
          ),
        );
        return constraints.maxWidth > 700
            ? Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: image),
                  Expanded(child: SingleChildScrollView(child: details)),
                ],
              )
            : ListView(children: [image, details]);
      }),
    );
  }
}
