import 'package:flutter/material.dart';

import '../../../core/state/async_controller.dart';
import '../../../core/widgets/state_views.dart';
import '../data/promotion_repository.dart';
import '../models/promotion_models.dart';
import '../widgets/promotion_widgets.dart';
import 'promotion_detail_screen.dart';

/// Grid columns for the promotions list: 1 on phones, 2 on small tablets,
/// 3 on wide screens.
int promotionGridColumns(double width) => width >= 900 ? 3 : (width >= 600 ? 2 : 1);

/// Lists the promotions that are live right now.
class PromotionsScreen extends StatefulWidget {
  const PromotionsScreen({super.key, required this.repository});

  final PromotionRepository repository;

  @override
  State<PromotionsScreen> createState() => _PromotionsScreenState();
}

class _PromotionsScreenState extends State<PromotionsScreen> {
  late final AsyncController<List<Promotion>> _controller;

  @override
  void initState() {
    super.initState();
    _controller = AsyncController(widget.repository.fetchActivePromotions);
    _controller.load();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _open(Promotion promotion) {
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
    return Scaffold(
      appBar: AppBar(title: const Text('Promotions')),
      body: AsyncStateView<List<Promotion>>(
        controller: _controller,
        loadingLabel: 'Loading promotions…',
        isEmpty: (promotions) => promotions.isEmpty,
        empty: const EmptyView(
          title: 'No promotions right now',
          message: 'Check back soon for new offers.',
        ),
        builder: (context, promotions) => RefreshIndicator(
          onRefresh: _controller.refresh,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final columns = promotionGridColumns(constraints.maxWidth);

              if (columns == 1) {
                return ListView.separated(
                  key: const Key('promotions-list'),
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  itemCount: promotions.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (context, index) => PromotionCard(
                    promotion: promotions[index],
                    onTap: () => _open(promotions[index]),
                  ),
                );
              }

              return GridView.builder(
                key: const Key('promotions-grid'),
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: columns,
                  mainAxisExtent: 220,
                  crossAxisSpacing: 12,
                  mainAxisSpacing: 12,
                ),
                itemCount: promotions.length,
                itemBuilder: (context, index) => PromotionCard(
                  promotion: promotions[index],
                  onTap: () => _open(promotions[index]),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}
