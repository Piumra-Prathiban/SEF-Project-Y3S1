import 'package:flutter_test/flutter_test.dart';
import 'package:sef_project/core/format/formatters.dart';
import 'package:sef_project/features/promotions/models/promotion_models.dart';
import 'package:sef_project/features/promotions/widgets/promotion_widgets.dart';

void main() {
  group('formatMoney', () {
    test('groups thousands and keeps two decimals', () {
      expect(formatMoney(1200), 'LKR 1,200.00');
      expect(formatMoney(1234567.5), 'LKR 1,234,567.50');
      expect(formatMoney(960), 'LKR 960.00');
      expect(formatMoney(0.5), 'LKR 0.50');
      expect(formatMoney(-50), '-LKR 50.00');
    });

    test('uses the currency from the API', () {
      expect(formatMoney(10, currency: 'USD'), 'USD 10.00');
    });
  });

  group('dates', () {
    test('formats UTC calendar dates', () {
      expect(formatDate(DateTime.utc(2026, 10, 15, 23)), '15 Oct 2026');
    });

    test('formats ranges within and across years', () {
      expect(
        formatDateRange(DateTime.utc(2026, 9, 1), DateTime.utc(2026, 12, 31)),
        '1 Sep – 31 Dec 2026',
      );
      expect(
        formatDateRange(DateTime.utc(2026, 12, 1), DateTime.utc(2027, 1, 31)),
        '1 Dec 2026 – 31 Jan 2027',
      );
    });
  });

  test('formatPlainNumber drops trailing zeros', () {
    expect(formatPlainNumber(20), '20');
    expect(formatPlainNumber(12.5), '12.5');
    expect(formatPlainNumber(100), '100');
  });

  test('discountLabel describes each promotion type', () {
    expect(discountLabel(PromotionType.percentageDiscount, 20), '20% off');
    expect(discountLabel(PromotionType.percentageDiscount, 12.5), '12.5% off');
    expect(discountLabel(PromotionType.fixedAmountDiscount, 50), 'LKR 50.00 off');
    expect(discountLabel(PromotionType.freeShipping, 0), 'Free delivery');
    expect(discountLabel(PromotionType.unknown, 0), 'Special offer');
  });
}
