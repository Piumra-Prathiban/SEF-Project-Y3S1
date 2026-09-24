import 'package:intl/intl.dart';

// Formatting is pinned to en_US so money and dates render identically on every
// device and in tests, while the currency code still comes from the API.
String formatCurrency(num amount, String currency) {
  return NumberFormat.currency(
    locale: 'en_US',
    symbol: '$currency ',
    decimalDigits: 2,
  ).format(amount);
}

String formatDate(DateTime date) =>
    DateFormat('d MMM yyyy', 'en_US').format(date.toLocal());

String formatDateTime(DateTime date) =>
    DateFormat('d MMM yyyy, HH:mm', 'en_US').format(date.toLocal());
