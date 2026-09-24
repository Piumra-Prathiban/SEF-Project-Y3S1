import 'package:flutter/material.dart';

import '../../../core/state/async_controller.dart';
import '../../../core/widgets/state_views.dart';
import '../data/promotion_repository.dart';
import '../models/promotion_models.dart';
import '../widgets/promotion_widgets.dart';
import 'promotion_detail_screen.dart';

/// A product with its live promotions. Prices are the API's best offer per
/// variant; the app never calculates discounts itself.
class ProductDetailScreen extends StatefulWidget {
  const ProductDetailScreen({
    super.key,
    required this.repository,
    required this.productId,
  });

  final PromotionRepository repository;
  final String productId;

  @override
  State<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends State<ProductDetailScreen> {
  late final AsyncController<ProductPromotions> _controller;

  @override
  void initState() {
    super.initState();
    _controller = AsyncController(
      () => widget.repository.fetchProductPromotions(widget.productId),
    );
    _controller.load();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _openPromotion(PromotionSummary promotion) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => PromotionDetailScreen(
          repository: widget.repository,
          promotionId: promotion.id,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Product')),
      body: AsyncStateView<ProductPromotions>(
        controller: _controller,
        loadingLabel: 'Loading product…',
        builder: (context, product) => RefreshIndicator(
          onRefresh: _controller.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              ContentWidth(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(product.productName, style: theme.textTheme.headlineSmall),
                    if (product.hasActivePromotion) ...[
                      const SizedBox(height: 8),
                      const PromotionIndicator(),
                    ],
                    if (product.description != null && product.description!.isNotEmpty) ...[
                      const SizedBox(height: 12),
                      Text(product.description!),
                    ],
                    const SizedBox(height: 20),
                    Text('Prices', style: theme.textTheme.titleLarge),
                    const SizedBox(height: 4),
                    if (product.variants.isEmpty)
                      const Text('This product is not available right now.'),
                    for (final variant in product.variants) VariantPriceRow(offer: variant),
                    const SizedBox(height: 4),
                    Text(
                      'Prices are set by the store and confirmed at checkout.',
                      style: theme.textTheme.bodySmall,
                    ),
                    const SizedBox(height: 20),
                    Text('Active promotions', style: theme.textTheme.titleLarge),
                    const SizedBox(height: 4),
                    if (product.promotions.isEmpty)
                      const Text('No active promotions for this product.'),
                    for (final promotion in product.promotions)
                      Card(
                        child: ListTile(
                          title: Text(promotion.name),
                          subtitle: ValidityText(start: promotion.startDate, end: promotion.endDate),
                          trailing: DiscountBadge(type: promotion.type, value: promotion.discountValue),
                          onTap: () => _openPromotion(promotion),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
