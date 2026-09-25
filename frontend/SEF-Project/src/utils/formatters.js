export function formatCurrency(value, currency = 'LKR') {
  return new Intl.NumberFormat('en-LK', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(value ?? 0);
}
