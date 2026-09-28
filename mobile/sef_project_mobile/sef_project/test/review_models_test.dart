import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/shopping_models.dart';

import 'support/fixtures.dart';

void main() {
  test('ProductReviews.fromJson parses aggregate, breakdown, and reviews', () {
    final result = ProductReviews.fromJson(productReviewsJson);

    expect(result.productId, 'product-1');
    expect(result.aggregate.averageRating, 4.5);
    expect(result.aggregate.totalCount, 2);
    expect(result.aggregate.breakdown.items, hasLength(5));
    expect(result.aggregate.breakdown.items.first.rating, 5);
    expect(result.aggregate.breakdown.items.first.count, 1);

    final review = result.reviews.first;
    expect(review.id, 'review-1');
    expect(review.productId, 'product-1');
    expect(review.displayName, 'Asha P.');
    expect(review.rating, 5);
    expect(review.comment, 'Lovely fit.');
    expect(review.isPublished, isTrue);
    expect(review.createdAt, DateTime.utc(2026, 9, 27, 10, 30));
    expect(review.updatedAt, DateTime.utc(2026, 9, 27, 10, 30));
  });

  test('ProductReview.fromJson preserves nullable comments', () {
    final result = ProductReviews.fromJson(productReviewsJson);

    expect(result.reviews.last.comment, isNull);
    expect(result.reviews.last.updatedAt, DateTime.utc(2026, 9, 26, 9));
  });

  test('ReviewAggregate.fromJson accepts an empty API aggregate', () {
    final aggregate = ReviewAggregate.fromJson(const {
      'averageRating': 0,
      'totalCount': 0,
      'breakdown': {'items': <Map<String, dynamic>>[]},
    });

    expect(aggregate.averageRating, 0);
    expect(aggregate.totalCount, 0);
    expect(aggregate.breakdown.items, isEmpty);
  });
}
