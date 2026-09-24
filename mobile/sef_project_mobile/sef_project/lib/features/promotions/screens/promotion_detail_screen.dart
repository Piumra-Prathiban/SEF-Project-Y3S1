import 'package:flutter/material.dart';

import '../../../core/state/async_controller.dart';
import '../../../core/widgets/state_views.dart';
import '../data/promotion_repository.dart';
import '../models/promotion_models.dart';
import '../widgets/promotion_widgets.dart';
import 'product_detail_screen.dart';

typedef PromotionDetail = ({Promotion promotion, PromotionProducts offers});

/// A promotion with its eligible products and their server-calculated prices.
class PromotionDetailScreen extends StatefulWidget {
  const PromotionDetailScreen({
    super.key,
    required this.repository,
    required this.promotionId,
  });

  final PromotionRepository repository;
  final String promotionId;

  @override
  State<PromotionDetailScreen> createState() => _PromotionDetailScreenState();
}

class _PromotionDetailScreenState extends State<PromotionDetailScreen> {
  late final AsyncController<PromotionDetail> _controller;

  @override
  void initState() {
    super.initState();
    _controller = AsyncController(_load);
    _controller.load();
  }

  Future<PromotionDetail> _load() async {
    // Future.wait rethrows the first failure as-is (e.g. the API's 404).
    final results = await Future.wait<Object>([
      widget.repository.fetchPromotion(widget.promotionId),
      widget.repository.fetchPromotionProducts(widget.promotionId),
    ]);

    return (promotion: results[0] as Promotion, offers: results[1] as PromotionProducts);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _openProduct(ProductOffer product) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => ProductDetailScreen(
          repository: widget.repository,
          productId: product.productId,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Promotion')),
      body: AsyncStateView<PromotionDetail>(
        controller: _controller,
        loadingLabel: 'Loading promotion…',
        builder: (context, detail) => RefreshIndicator(
          onRefresh: _controller.refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              ContentWidth(child: _PromotionHeader(promotion: detail.promotion)),
              if (!detail.offers.hasPriceDiscount)
                const ContentWidth(
                  child: Card(
                    child: ListTile(
                      leading: Icon(Icons.info_outline),
                      title: Text('Applied at checkout. Product prices are not changed by this offer.'),
                    ),
                  ),
                ),
              const SizedBox(height: 16),
              ContentWidth(
                child: Text('Eligible products', style: Theme.of(context).textTheme.titleLarge),
              ),
              const SizedBox(height: 8),
              if (detail.offers.products.isEmpty)
                const ContentWidth(
                  child: Text('No products are currently included in this promotion.'),
                ),
              for (final product in detail.offers.products)
                ContentWidth(
                  child: _ProductOfferCard(
                    product: product,
                    onTap: () => _openProduct(product),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PromotionHeader extends StatelessWidget {
  const _PromotionHeader({required this.promotion});

  final Promotion promotion;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(promotion.name, style: theme.textTheme.headlineSmall),
        const SizedBox(height: 8),
        DiscountBadge(type: promotion.type, value: promotion.discountValue),
        if (promotion.campaignName != null) ...[
          const SizedBox(height: 8),
          Text('Part of ${promotion.campaignName}', style: theme.textTheme.bodySmall),
        ],
        if (promotion.description != null && promotion.description!.isNotEmpty) ...[
          const SizedBox(height: 12),
          Text(promotion.description!),
        ],
        const SizedBox(height: 12),
        ValidityText(start: promotion.startDate, end: promotion.endDate),
      ],
    );
  }
}

class _ProductOfferCard extends StatelessWidget {
  const _ProductOfferCard({required this.product, required this.onTap});

  final ProductOffer product;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(product.productName, style: Theme.of(context).textTheme.titleMedium),
                  ),
                  if (product.hasDiscount) const PromotionIndicator(),
                  const Icon(Icons.chevron_right),
                ],
              ),
              for (final variant in product.variants) VariantPriceRow(offer: variant),
            ],
          ),
        ),
      ),
    );
  }
}
