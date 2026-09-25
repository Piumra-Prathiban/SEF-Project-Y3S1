/// Display formatting only. Prices and discounts shown in the app always come
/// from the API; nothing here calculates them.

const List<String> _months = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

/// `LKR 1,234.50`
String formatMoney(num value, {String currency = 'LKR'}) {
  final negative = value < 0;
  final fixed = value.abs().toStringAsFixed(2);
  final parts = fixed.split('.');
  final digits = parts[0];
  final grouped = StringBuffer();

  for (var i = 0; i < digits.length; i++) {
    if (i > 0 && (digits.length - i) % 3 == 0) {
      grouped.write(',');
    }
    grouped.write(digits[i]);
  }

  return '${negative ? '-' : ''}$currency $grouped.${parts[1]}';
}

/// `15 Oct 2026` (UTC calendar date, matching the API).
String formatDate(DateTime date) {
  final utc = date.toUtc();
  return '${utc.day} ${_months[utc.month - 1]} ${utc.year}';
}

/// `1 Sep – 31 Dec 2026`, or with both years when they differ.
String formatDateRange(DateTime start, DateTime end) {
  final s = start.toUtc();
  final e = end.toUtc();

  if (s.year == e.year) {
    return '${s.day} ${_months[s.month - 1]} – ${formatDate(e)}';
  }
  return '${formatDate(s)} – ${formatDate(e)}';
}

/// `20`, `12.5` — drops a trailing `.0`.
String formatPlainNumber(num value) {
  final text = value.toStringAsFixed(2);
  return text.replaceFirst(RegExp(r'\.?0+$'), '');
}
