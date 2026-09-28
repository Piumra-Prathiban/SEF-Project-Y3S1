import 'package:flutter/material.dart';

import '../models/shopping_models.dart';
import '../state/customer_store.dart';
import '../utils/formatters.dart';
import 'common.dart';

class ReviewsSection extends StatefulWidget {
  const ReviewsSection({super.key, required this.productId});

  final String productId;

  @override
  State<ReviewsSection> createState() => _ReviewsSectionState();
}

class _ReviewsSectionState extends State<ReviewsSection> {
  final _comment = TextEditingController();
  bool _loaded = false;
  bool _formSynced = false;
  int _rating = 0;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_loaded) return;
    _loaded = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      StoreScope.of(context).loadReviews(widget.productId).catchError((_) {});
    });
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final store = StoreScope.of(context);
    final data = store.productReviews;
    if (!_formSynced && data?.productId == widget.productId && !store.loading) {
      _formSynced = true;
      _rating = store.ownReview?.rating ?? 0;
      _comment.text = store.ownReview?.comment ?? '';
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Customer reviews', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        AsyncPanel(
          loading: store.loading && data == null,
          error: store.error,
          onRetry: () => store.loadReviews(widget.productId),
          child: data == null
              ? const SizedBox.shrink()
              : _ReviewsContent(
                  data: data,
                  authenticated: store.authenticated,
                  ownReview: store.ownReview,
                  rating: _rating,
                  comment: _comment,
                  busy: store.loading,
                  onRatingChanged: (value) => setState(() => _rating = value),
                  onSubmit: _rating == 0 ? null : () => _submit(store),
                  onDelete: store.ownReview == null
                      ? null
                      : () => _delete(store),
                ),
        ),
      ],
    );
  }

  Future<void> _submit(CustomerStore store) async {
    final comment = _comment.text.trim();
    await runAction(
      context,
      () => store.saveReview(
        widget.productId,
        rating: _rating,
        comment: comment.isEmpty ? null : comment,
      ),
      success: 'Review saved.',
    );
  }

  Future<void> _delete(CustomerStore store) async {
    await runAction(
      context,
      () => store.deleteReview(widget.productId),
      success: 'Review deleted.',
    );
    if (!mounted || store.ownReview != null) return;
    setState(() {
      _rating = 0;
      _comment.clear();
    });
  }
}

class _ReviewsContent extends StatelessWidget {
  const _ReviewsContent({
    required this.data,
    required this.authenticated,
    required this.ownReview,
    required this.rating,
    required this.comment,
    required this.busy,
    required this.onRatingChanged,
    required this.onSubmit,
    required this.onDelete,
  });

  final ProductReviews data;
  final bool authenticated;
  final ProductReview? ownReview;
  final int rating;
  final TextEditingController comment;
  final bool busy;
  final ValueChanged<int> onRatingChanged;
  final VoidCallback? onSubmit;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      _ReviewSummary(aggregate: data.aggregate),
      const SizedBox(height: 20),
      if (authenticated)
        _ReviewForm(
          ownReview: ownReview,
          rating: rating,
          comment: comment,
          busy: busy,
          onRatingChanged: onRatingChanged,
          onSubmit: onSubmit,
          onDelete: onDelete,
        )
      else
        const Card(
          child: Padding(
            padding: EdgeInsets.all(16),
            child: Text('Sign in to review this product.'),
          ),
        ),
      const SizedBox(height: 20),
      Text('Published reviews', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8),
      AsyncPanel(
        empty: data.reviews.isEmpty,
        emptyTitle: 'No reviews yet',
        emptyMessage: 'Be the first customer to review this product.',
        child: Column(
          children: [for (final review in data.reviews) _ReviewCard(review)],
        ),
      ),
    ],
  );
}

class _ReviewSummary extends StatelessWidget {
  const _ReviewSummary({required this.aggregate});

  final ReviewAggregate aggregate;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${aggregate.averageRating.toStringAsFixed(1)} out of 5',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          Text(
            '${aggregate.totalCount} ${aggregate.totalCount == 1 ? 'review' : 'reviews'}',
          ),
          const SizedBox(height: 12),
          for (final item in aggregate.breakdown.items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                children: [
                  SizedBox(width: 52, child: Text('${item.rating} stars')),
                  const SizedBox(width: 8),
                  Expanded(
                    child: LinearProgressIndicator(
                      value: aggregate.totalCount == 0
                          ? 0
                          : item.count / aggregate.totalCount,
                    ),
                  ),
                  const SizedBox(width: 8),
                  SizedBox(width: 24, child: Text('${item.count}')),
                ],
              ),
            ),
        ],
      ),
    ),
  );
}

class _ReviewForm extends StatelessWidget {
  const _ReviewForm({
    required this.ownReview,
    required this.rating,
    required this.comment,
    required this.busy,
    required this.onRatingChanged,
    required this.onSubmit,
    required this.onDelete,
  });

  final ProductReview? ownReview;
  final int rating;
  final TextEditingController comment;
  final bool busy;
  final ValueChanged<int> onRatingChanged;
  final VoidCallback? onSubmit;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            ownReview == null ? 'Write a review' : 'Your review',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          Wrap(
            children: [
              for (var star = 1; star <= 5; star++)
                IconButton(
                  key: Key('review-rating-$star'),
                  tooltip: '$star ${star == 1 ? 'star' : 'stars'}',
                  onPressed: busy ? null : () => onRatingChanged(star),
                  icon: Icon(
                    star <= rating ? Icons.star : Icons.star_border,
                    color: Colors.amber.shade700,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          TextField(
            key: const Key('review-comment'),
            controller: comment,
            enabled: !busy,
            maxLength: 2000,
            minLines: 2,
            maxLines: 4,
            decoration: const InputDecoration(labelText: 'Comment (optional)'),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              FilledButton.icon(
                key: const Key('review-submit'),
                onPressed: busy ? null : onSubmit,
                icon: const Icon(Icons.rate_review_outlined),
                label: Text(
                  ownReview == null ? 'Submit review' : 'Update review',
                ),
              ),
              if (onDelete != null)
                OutlinedButton.icon(
                  key: const Key('review-delete'),
                  onPressed: busy ? null : onDelete,
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Delete review'),
                ),
            ],
          ),
        ],
      ),
    ),
  );
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard(this.review);

  final ProductReview review;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  review.displayName,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              Text(formatDate(review.createdAt)),
            ],
          ),
          const SizedBox(height: 4),
          Semantics(
            label: '${review.rating} out of 5 stars',
            child: Row(
              children: [
                for (var star = 1; star <= 5; star++)
                  Icon(
                    star <= review.rating ? Icons.star : Icons.star_border,
                    size: 18,
                    color: Colors.amber.shade700,
                  ),
              ],
            ),
          ),
          if (review.comment?.isNotEmpty == true) ...[
            const SizedBox(height: 8),
            Text(review.comment!),
          ],
          const SizedBox(height: 12),
        ],
      ),
    ),
  );
}
