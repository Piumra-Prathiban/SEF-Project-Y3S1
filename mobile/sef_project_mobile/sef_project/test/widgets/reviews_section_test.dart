import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/models/shopping_models.dart';
import 'package:sef_project/services/customer_api.dart';
import 'package:sef_project/state/customer_store.dart';
import 'package:sef_project/widgets/reviews_section.dart';

import '../fake_customer_repository.dart';

Widget _screen(CustomerStore store) => StoreScope(
  store: store,
  child: const MaterialApp(
    home: Scaffold(
      body: SingleChildScrollView(
        padding: EdgeInsets.all(16),
        child: ReviewsSection(productId: 'product-1'),
      ),
    ),
  ),
);

void main() {
  testWidgets('renders the review aggregate and published reviews', (
    tester,
  ) async {
    final store = CustomerStore(FakeCustomerRepository());

    await tester.pumpWidget(_screen(store));
    await tester.pumpAndSettle();

    expect(find.text('4.5 out of 5'), findsOneWidget);
    expect(find.text('2 reviews'), findsOneWidget);
    expect(find.text('5 stars'), findsOneWidget);
    expect(find.text('Asha P.'), findsOneWidget);
    expect(find.text('Lovely fit.'), findsOneWidget);
    expect(find.text('Sign in to review this product.'), findsOneWidget);
  });

  testWidgets('renders the published reviews empty state', (tester) async {
    final repository = FakeCustomerRepository()
      ..currentProductReviews = const ProductReviews(
        productId: 'product-1',
        aggregate: ReviewAggregate(
          averageRating: 0,
          totalCount: 0,
          breakdown: ReviewBreakdown(items: []),
        ),
        reviews: [],
      );

    await tester.pumpWidget(_screen(CustomerStore(repository)));
    await tester.pumpAndSettle();

    expect(find.text('0.0 out of 5'), findsOneWidget);
    expect(find.text('No reviews yet'), findsOneWidget);
    expect(
      find.text('Be the first customer to review this product.'),
      findsOneWidget,
    );
  });

  testWidgets('renders an error and retries the review request', (
    tester,
  ) async {
    final repository = FakeCustomerRepository()
      ..reviewError = const ApiException('Reviews are unavailable.');
    final store = CustomerStore(repository);

    await tester.pumpWidget(_screen(store));
    await tester.pumpAndSettle();

    expect(find.text('Reviews are unavailable.'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(repository.reviewCalls, 1);

    repository.reviewError = null;
    await tester.tap(find.text('Try again'));
    await tester.pumpAndSettle();

    expect(repository.reviewCalls, 2);
    expect(find.text('4.5 out of 5'), findsOneWidget);
    expect(find.text('Reviews are unavailable.'), findsNothing);
  });

  testWidgets('submits the selected rating and trimmed comment', (
    tester,
  ) async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository)..authenticated = true;

    await tester.pumpWidget(_screen(store));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('review-rating-4')));
    await tester.enterText(
      find.byKey(const Key('review-comment')),
      '  Comfortable and well made.  ',
    );
    final submit = find.byKey(const Key('review-submit'));
    await tester.ensureVisible(submit);
    await tester.tap(submit);
    await tester.pumpAndSettle();

    expect(repository.lastReviewRating, 4);
    expect(repository.lastReviewComment, 'Comfortable and well made.');
    expect(find.text('Review saved.'), findsOneWidget);
  });
}
