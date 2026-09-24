import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/services/customer_api.dart';
import 'package:sef_project/models/shopping_models.dart';
import 'package:sef_project/state/customer_store.dart';

import 'fake_customer_repository.dart';

void main() {
  test('discovery filters are sent to the repository', () async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await store.applyFilters(
      searchText: 'linen',
      category: 'category-1',
      minimum: 1000,
      maximum: 5000,
      availableOnly: true,
      orderBy: 'price',
      direction: 'desc',
    );

    expect(repository.lastSearch, 'linen');
    expect(repository.lastCategoryId, 'category-1');
    expect(repository.lastMinPrice, 1000);
    expect(repository.lastMaxPrice, 5000);
    expect(repository.lastInStockOnly, isTrue);
    expect(repository.lastSortBy, 'price');
    expect(repository.lastSortDirection, 'desc');
  });

  test('invalid price range is rejected before an API call', () async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await expectLater(
      store.applyFilters(
        searchText: '',
        minimum: 5000,
        maximum: 1000,
        availableOnly: false,
        orderBy: 'name',
        direction: 'asc',
      ),
      throwsA(isA<ApiException>()),
    );
  });

  test('cart state always uses repository-calculated totals', () async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await store.addToCart('variant-1', 2);

    expect(store.cart.totalQuantity, 2);
    expect(store.cart.subtotal, 9000);
    expect(store.cart.total, 9000);
  });

  test('saving a default address refreshes the single-default state', () async {
    final repository = FakeCustomerRepository();
    final store = CustomerStore(repository);

    await store.saveAddress(
      const CustomerAddress(
        label: 'Office',
        addressLine1: '20 Work Road',
        city: 'Colombo',
        postalCode: '00200',
        country: 'Sri Lanka',
        isDefault: true,
      ),
    );

    expect(store.addresses.where((address) => address.isDefault), hasLength(1));
    expect(
      store.addresses.singleWhere((address) => address.isDefault).label,
      'Office',
    );
  });

  test(
    'recommendations retain authoritative result and hydrate product details',
    () async {
      final repository = FakeCustomerRepository();
      final store = CustomerStore(repository);

      await store.requestRecommendations(
        const RecommendationPreferences(
          occasion: 'Dinner',
          budget: 20000,
          preferredColours: ['Blue'],
          preferredSize: 'M',
        ),
      );

      expect(store.recommendationStatus, RecommendationUiStatus.completed);
      expect(store.recommendation?.workflowId, 'workflow-1');
      expect(store.recommendation?.recommendations.single.price, 4500);
      expect(store.recommendationProduct('product-1')?.name, 'Linen Shirt');
      expect(repository.lastRecommendationPreferences?.occasion, 'Dinner');
    },
  );

  test(
    'failed deterministic validation is shown as validation failure',
    () async {
      final repository = FakeCustomerRepository()
        ..recommendationResult = const RecommendationResult(
          workflowId: 'workflow-failed',
          status: 'failed',
          recommendations: [],
          execution: RecommendationExecution(
            agentName: 'Personal Stylist Agent',
            status: 'failed',
            outputValidated: false,
            errorSummary: 'Agent output failed catalogue validation.',
            validationResults: [
              RecommendationValidation(
                rule: 'Stock',
                isValid: false,
                message: 'Insufficient stock.',
              ),
            ],
          ),
        );
      final store = CustomerStore(repository);

      await store.requestRecommendations(
        const RecommendationPreferences(occasion: 'Dinner'),
      );

      expect(
        store.recommendationStatus,
        RecommendationUiStatus.validationFailure,
      );
      expect(store.recommendation?.recommendations, isEmpty);
    },
  );

  test('recommendation timeout has a distinct retryable state', () async {
    final repository = FakeCustomerRepository()
      ..recommendationError = TimeoutException('Timed out');
    final store = CustomerStore(repository);

    await store.requestRecommendations(
      const RecommendationPreferences(occasion: 'Dinner'),
    );

    expect(store.recommendationStatus, RecommendationUiStatus.timeout);
    expect(store.recommendationMessage, contains('timed out'));
  });
}
