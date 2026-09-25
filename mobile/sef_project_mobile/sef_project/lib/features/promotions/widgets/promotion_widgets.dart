import 'package:flutter/material.dart';

import '../../../core/format/formatters.dart';
import '../models/promotion_models.dart';

/// Describes a promotion's configured offer, e.g. "20% off" (display only).
String discountLabel(PromotionType type, double value) => switch (type) {
      PromotionType.percentageDiscount => '${formatPlainNumber(value)}% off',
      PromotionType.fixedAmountDiscount => '${formatMoney(value)} off',
      PromotionType.freeShipping => 'Free delivery',
      PromotionType.buyXGetY => 'Buy X get Y',
      PromotionType.unknown => 'Special offer',
    };

class DiscountBadge extends StatelessWidget {
  const DiscountBadge({super.key, required this.type, required this.value});

  final PromotionType type;
  final double value;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: scheme.primaryContainer,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        child: Text(
          discountLabel(type, value),
          style: TextStyle(
            color: scheme.onPrimaryContainer,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

/// "On promotion" marker: icon + text, never colour alone.
class PromotionIndicator extends StatelessWidget {
  const PromotionIndicator({super.key, this.label = 'On promotion'});

  final String label;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Semantics(
      label: label,
      excludeSemantics: true,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: scheme.tertiaryContainer,
          borderRadius: BorderRadius.circular(6),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.local_offer, size: 16, color: scheme.onTertiaryContainer),
              const SizedBox(width: 4),
              Text(
                label,
                style: TextStyle(color: scheme.onTertiaryContainer, fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Shows the server's final price, with the original struck through when a
/// discount applies. Never computes a price itself.
class PriceTag extends StatelessWidget {
  const PriceTag({super.key, required this.offer});

  final VariantOffer offer;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final finalText = formatMoney(offer.finalPrice, currency: offer.currency);

    if (!offer.isDiscounted) {
      return Text(finalText, style: theme.textTheme.titleMedium);
    }

    final originalText = formatMoney(offer.originalPrice, currency: offer.currency);

    return Semantics(
      label: 'Now $finalText, was $originalText',
      excludeSemantics: true,
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 8,
        children: [
          Text(
            finalText,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.bold,
              color: theme.colorScheme.primary,
            ),
          ),
          Text(
            originalText,
            style: theme.textTheme.bodyMedium?.copyWith(
              decoration: TextDecoration.lineThrough,
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

/// One variant row: name, SKU and its server price.
class VariantPriceRow extends StatelessWidget {
  const VariantPriceRow({super.key, required this.offer});

  final VariantOffer offer;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(offer.name),
                Text(offer.sku, style: Theme.of(context).textTheme.bodySmall),
                if (offer.isDiscounted && offer.promotionName != null)
                  Text(
                    '${offer.promotionName} applied',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
              ],
            ),
          ),
          PriceTag(offer: offer),
        ],
      ),
    );
  }
}

class PromotionCard extends StatelessWidget {
  const PromotionCard({super.key, required this.promotion, required this.onTap});

  final Promotion promotion;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                promotion.name,
                style: theme.textTheme.titleMedium,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 8),
              DiscountBadge(type: promotion.type, value: promotion.discountValue),
              if (promotion.description != null && promotion.description!.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(promotion.description!, maxLines: 2, overflow: TextOverflow.ellipsis),
              ],
              const SizedBox(height: 8),
              ValidityText(start: promotion.startDate, end: promotion.endDate),
            ],
          ),
        ),
      ),
    );
  }
}

class ValidityText extends StatelessWidget {
  const ValidityText({super.key, required this.start, required this.end});

  final DateTime start;
  final DateTime end;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.event, size: 16, color: Theme.of(context).colorScheme.onSurfaceVariant),
        const SizedBox(width: 4),
        Flexible(
          child: Text(
            'Valid ${formatDateRange(start, end)}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ),
      ],
    );
  }
}

/// Centres content and caps its width on tablets and desktops.
class ContentWidth extends StatelessWidget {
  const ContentWidth({super.key, required this.child, this.maxWidth = 760});

  final Widget child;
  final double maxWidth;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: maxWidth),
        child: SizedBox(width: double.infinity, child: child),
      ),
    );
  }
}
