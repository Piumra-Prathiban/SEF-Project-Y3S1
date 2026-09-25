import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../widgets/common.dart';
import '../widgets/product_image.dart';
import 'product_detail_screen.dart';

class RecommendationsScreen extends StatefulWidget {
  const RecommendationsScreen({super.key});

  @override
  State<RecommendationsScreen> createState() => _RecommendationsScreenState();
}

class _RecommendationsScreenState extends State<RecommendationsScreen> {
  final _formKey = GlobalKey<FormState>();
  final _occasion = TextEditingController(text: 'Dinner');
  final _budget = TextEditingController(text: '20000');
  final _size = TextEditingController();
  final _colours = TextEditingController();
  final _style = TextEditingController();

  @override
  void dispose() {
    _occasion.dispose();
    _budget.dispose();
    _size.dispose();
    _colours.dispose();
    _style.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    final busy =
        store.recommendationStatus == RecommendationUiStatus.pending ||
        store.recommendationStatus == RecommendationUiStatus.processing;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Personal Stylist'),
        actions: [
          if (store.recommendationStatus != RecommendationUiStatus.idle)
            IconButton(
              tooltip: 'Start over',
              onPressed: busy ? null : store.resetRecommendations,
              icon: const Icon(Icons.refresh),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          Text(
            'Find an outfit from the real catalogue',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 6),
          const Text(
            'Tell us what you need. Recommendations are checked against current prices and stock before they are shown.',
          ),
          const SizedBox(height: 18),
          _PreferenceForm(
            formKey: _formKey,
            occasion: _occasion,
            budget: _budget,
            size: _size,
            colours: _colours,
            style: _style,
            busy: busy,
            onSubmit: _submit,
          ),
          const SizedBox(height: 20),
          _RecommendationContent(store: store, onRetry: _submit),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    if (_formKey.currentState?.validate() != true) return;

    final colours = _colours.text
        .split(RegExp(r'[,/]'))
        .map((item) => item.trim())
        .where((item) => item.isNotEmpty)
        .toSet()
        .toList();
    await StoreScope.of(context).requestRecommendations(
      RecommendationPreferences(
        occasion: _occasion.text.trim(),
        budget: _budget.text.trim().isEmpty
            ? null
            : double.parse(_budget.text.trim()),
        preferredColours: colours,
        preferredSize: _optional(_size.text),
        stylePreferences: _optional(_style.text),
      ),
    );
  }

  static String? _optional(String value) {
    final normalized = value.trim();
    return normalized.isEmpty ? null : normalized;
  }
}

class _PreferenceForm extends StatelessWidget {
  const _PreferenceForm({
    required this.formKey,
    required this.occasion,
    required this.budget,
    required this.size,
    required this.colours,
    required this.style,
    required this.busy,
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController occasion;
  final TextEditingController budget;
  final TextEditingController size;
  final TextEditingController colours;
  final TextEditingController style;
  final bool busy;
  final Future<void> Function() onSubmit;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              key: const Key('recommendation-occasion'),
              controller: occasion,
              enabled: !busy,
              decoration: const InputDecoration(
                labelText: 'Occasion',
                hintText: 'Dinner, wedding, work…',
              ),
              textInputAction: TextInputAction.next,
              validator: (value) {
                final length = value?.trim().length ?? 0;
                return length < 2 || length > 100
                    ? 'Enter an occasion between 2 and 100 characters.'
                    : null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('recommendation-budget'),
              controller: budget,
              enabled: !busy,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Total budget (LKR)',
                prefixText: 'Rs. ',
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return null;
                final amount = double.tryParse(value.trim());
                return amount == null || amount <= 0
                    ? 'Enter a valid positive budget.'
                    : null;
              },
            ),
            const SizedBox(height: 12),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: TextFormField(
                    key: const Key('recommendation-size'),
                    controller: size,
                    enabled: !busy,
                    decoration: const InputDecoration(
                      labelText: 'Preferred size',
                      hintText: 'M',
                    ),
                    validator: (value) => (value?.trim().length ?? 0) > 50
                        ? 'Maximum 50 characters.'
                        : null,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    key: const Key('recommendation-colours'),
                    controller: colours,
                    enabled: !busy,
                    decoration: const InputDecoration(
                      labelText: 'Preferred colours',
                      hintText: 'Black / White',
                    ),
                    validator: (value) {
                      final entries = (value ?? '')
                          .split(RegExp(r'[,/]'))
                          .map((item) => item.trim())
                          .where((item) => item.isNotEmpty)
                          .toList();
                      if (entries.length > 10) {
                        return 'Enter no more than 10 colours.';
                      }
                      if (entries.any((item) => item.length > 50)) {
                        return 'Each colour must be 50 characters or fewer.';
                      }
                      return null;
                    },
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('recommendation-style'),
              controller: style,
              enabled: !busy,
              maxLength: 500,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Style preferences (optional)',
                hintText: 'Minimal, formal, relaxed…',
              ),
            ),
            const SizedBox(height: 4),
            FilledButton.icon(
              key: const Key('recommendation-submit'),
              onPressed: busy ? null : onSubmit,
              icon: const Icon(Icons.auto_awesome),
              label: const Text('Ask Personal Stylist'),
            ),
          ],
        ),
      ),
    ),
  );
}

class _RecommendationContent extends StatelessWidget {
  const _RecommendationContent({required this.store, required this.onRetry});

  final CustomerStore store;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    switch (store.recommendationStatus) {
      case RecommendationUiStatus.idle:
        return const _InfoPanel(
          icon: Icons.tips_and_updates_outlined,
          title: 'Your recommendations will appear here',
          message: 'Nothing is added to your wishlist or cart automatically.',
        );
      case RecommendationUiStatus.pending:
        return const _ProgressPanel(
          title: 'Request pending',
          message: 'Preparing your recommendation workflow…',
        );
      case RecommendationUiStatus.processing:
        return const _ProgressPanel(
          title: 'Personal Stylist is working',
          message: 'Searching products and validating price and availability…',
        );
      case RecommendationUiStatus.completed:
        return _CompletedRecommendations(store: store);
      case RecommendationUiStatus.validationFailure:
        final failures =
            store.recommendation?.execution.validationResults
                .where((item) => !item.isValid)
                .toList() ??
            const <RecommendationValidation>[];
        return _FailurePanel(
          icon: Icons.rule,
          title: 'Recommendations did not pass validation',
          message: failures.isEmpty
              ? store.recommendationMessage ??
                    'No unverified recommendations were returned.'
              : failures
                    .map((item) => '${item.rule}: ${item.message}')
                    .join('\n'),
          onRetry: onRetry,
        );
      case RecommendationUiStatus.safeFailure:
        return _FailurePanel(
          icon: Icons.shield_outlined,
          title: 'No safe recommendation available',
          message:
              store.recommendationMessage ??
              'The workflow stopped safely without returning products.',
          onRetry: onRetry,
        );
      case RecommendationUiStatus.timeout:
        return _FailurePanel(
          icon: Icons.timer_outlined,
          title: 'The stylist took too long',
          message:
              store.recommendationMessage ??
              'The recommendation request timed out. Please try again.',
          onRetry: onRetry,
        );
      case RecommendationUiStatus.error:
        return _FailurePanel(
          icon: Icons.cloud_off_outlined,
          title: 'Could not load recommendations',
          message: store.recommendationMessage ?? 'Please try again.',
          onRetry: onRetry,
        );
    }
  }
}

class _CompletedRecommendations extends StatelessWidget {
  const _CompletedRecommendations({required this.store});

  final CustomerStore store;

  @override
  Widget build(BuildContext context) {
    final result = store.recommendation!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _WorkflowStatus(result: result),
        const SizedBox(height: 16),
        if (result.recommendations.isEmpty)
          const _InfoPanel(
            icon: Icons.search_off,
            title: 'No matching products',
            message: 'Try a different occasion, budget, or fewer preferences.',
          )
        else ...[
          Text(
            '${result.recommendations.length} verified recommendation${result.recommendations.length == 1 ? '' : 's'}',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 10),
          LayoutBuilder(
            builder: (context, constraints) {
              final columns = constraints.maxWidth >= 760 ? 2 : 1;
              return GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: columns,
                  crossAxisSpacing: 12,
                  mainAxisSpacing: 12,
                  childAspectRatio: columns == 1 ? .68 : .76,
                ),
                itemCount: result.recommendations.length,
                itemBuilder: (context, index) => _RecommendationCard(
                  recommendation: result.recommendations[index],
                  product: store.recommendationProduct(
                    result.recommendations[index].productId,
                  ),
                ),
              );
            },
          ),
        ],
      ],
    );
  }
}

class _WorkflowStatus extends StatelessWidget {
  const _WorkflowStatus({required this.result});

  final RecommendationResult result;

  @override
  Widget build(BuildContext context) => Card(
    color: Theme.of(context).colorScheme.secondaryContainer,
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          const Icon(Icons.verified_outlined),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Workflow ${result.status}',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                Text('${result.execution.agentName} • ${result.workflowId}'),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class _RecommendationCard extends StatelessWidget {
  const _RecommendationCard({
    required this.recommendation,
    required this.product,
  });

  final ProductRecommendation recommendation;
  final Product? product;

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    final variantDetails = [
      recommendation.variantName,
      if (recommendation.size?.isNotEmpty == true)
        'Size ${recommendation.size}',
      if (recommendation.colour?.isNotEmpty == true) recommendation.colour!,
    ];
    final canAddToCart =
        recommendation.variantId.isNotEmpty &&
        recommendation.quantity > 0 &&
        recommendation.availableQuantity >= recommendation.quantity;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            height: 150,
            child: product == null
                ? const ColoredBox(
                    color: Color(0xffe2ebe5),
                    child: Center(child: Icon(Icons.image_outlined, size: 48)),
                  )
                : ProductImage(product: product!),
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    recommendation.productName,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(variantDetails.join(' / ')),
                  Text(
                    'SKU: ${recommendation.sku}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    money(recommendation.price),
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  Text('${recommendation.availableQuantity} available'),
                  const SizedBox(height: 8),
                  Expanded(
                    child: SingleChildScrollView(
                      child: Text(
                        recommendation.reason,
                        key: const Key('recommendation-reason'),
                      ),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      OutlinedButton(
                        onPressed: product == null
                            ? null
                            : () => Navigator.push<void>(
                                context,
                                MaterialPageRoute(
                                  builder: (_) => StoreScope(
                                    store: store,
                                    child: ProductDetailScreen(
                                      product: product!,
                                    ),
                                  ),
                                ),
                              ),
                        child: const Text('View product'),
                      ),
                      IconButton.filledTonal(
                        tooltip: 'Add recommended product to wishlist',
                        onPressed: recommendation.productId.isEmpty
                            ? null
                            : () => runAction(
                                context,
                                () => store.addToWishlist(
                                  recommendation.productId,
                                ),
                                success: 'Saved to wishlist.',
                              ),
                        icon: const Icon(Icons.favorite_border),
                      ),
                      IconButton.filled(
                        tooltip: 'Add recommended variant to cart',
                        onPressed: canAddToCart
                            ? () => runAction(
                                context,
                                () => store.addToCart(
                                  recommendation.variantId,
                                  recommendation.quantity,
                                ),
                                success: 'Added to cart.',
                              )
                            : null,
                        icon: const Icon(Icons.add_shopping_cart),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ProgressPanel extends StatelessWidget {
  const _ProgressPanel({required this.title, required this.message});

  final String title;
  final String message;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Row(
        children: [
          const CircularProgressIndicator(),
          const SizedBox(width: 18),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 4),
                Text(message),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class _InfoPanel extends StatelessWidget {
  const _InfoPanel({
    required this.icon,
    required this.title,
    required this.message,
  });

  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        children: [
          Icon(icon, size: 42),
          const SizedBox(height: 10),
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(message, textAlign: TextAlign.center),
        ],
      ),
    ),
  );
}

class _FailurePanel extends StatelessWidget {
  const _FailurePanel({
    required this.icon,
    required this.title,
    required this.message,
    required this.onRetry,
  });

  final IconData icon;
  final String title;
  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) => Card(
    color: Theme.of(context).colorScheme.errorContainer,
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Icon(icon, size: 40),
          const SizedBox(height: 8),
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 6),
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          FilledButton.tonal(
            onPressed: onRetry,
            child: const Text('Try again'),
          ),
        ],
      ),
    ),
  );
}
