// Presentation helpers only: every metric shown comes from the API.

export const RANGE_PRESETS = [
  { value: '7', label: 'Last 7 days' },
  { value: '30', label: 'Last 30 days' },
  { value: '90', label: 'Last 90 days' },
  { value: '365', label: 'Last 12 months' },
  { value: 'custom', label: 'Custom range' },
];

export const GRANULARITY = { DAY: 0, MONTH: 1 };

export const STOCK_STATUS = { IN_STOCK: 0, LOW_STOCK: 1, OUT_OF_STOCK: 2 };

export const STOCK_STATUS_LABELS = [
  { label: 'In stock', tone: 'success' },
  { label: 'Low stock', tone: 'warning' },
  { label: 'Out of stock', tone: 'danger' },
];

export const DEMAND_TRENDS = [
  { label: 'No sales', tone: 'muted', icon: '–' },
  { label: 'New', tone: 'info', icon: '★' },
  { label: 'Rising', tone: 'success', icon: '▲' },
  { label: 'Stable', tone: 'muted', icon: '■' },
  { label: 'Falling', tone: 'danger', icon: '▼' },
];

const DAY_MS = 24 * 60 * 60 * 1000;

function utcMidnight(date) {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

function toDateInput(date) {
  return date.toISOString().slice(0, 10);
}

/**
 * Converts the UI selection into the API's half-open UTC range [from, to).
 * Presets cover whole UTC days up to and including today; a custom "to" date
 * is inclusive for the user, so the API receives the following midnight.
 */
export function resolveDateRange({ range = '30', from = '', to = '' }, now = new Date()) {
  if (range === 'custom' && from && to && to >= from) {
    const start = new Date(`${from}T00:00:00Z`);
    const end = new Date(new Date(`${to}T00:00:00Z`).getTime() + DAY_MS);

    return { from: start.toISOString(), to: end.toISOString(), days: (end - start) / DAY_MS };
  }

  const days = Number(range) > 0 ? Number(range) : 30;
  const end = new Date(utcMidnight(now).getTime() + DAY_MS);
  const start = new Date(end.getTime() - days * DAY_MS);

  return { from: start.toISOString(), to: end.toISOString(), days };
}

// The period of the same length immediately before `range` (for comparisons).
export function previousRange({ from, to }) {
  const start = new Date(from);
  const span = new Date(to) - start;

  return {
    from: new Date(start.getTime() - span).toISOString(),
    to: start.toISOString(),
  };
}

// Daily points up to ~3 months, monthly beyond (the API caps daily at 366).
export function defaultGranularity(days) {
  return days <= 92 ? GRANULARITY.DAY : GRANULARITY.MONTH;
}

export function describeRange({ from, to }) {
  const format = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeZone: 'UTC' });
  const lastDay = new Date(new Date(to).getTime() - DAY_MS);

  return `${format.format(new Date(from))} – ${format.format(lastDay)}`;
}

export function customRangeDefaults(now = new Date()) {
  const today = utcMidnight(now);

  return {
    from: toDateInput(new Date(today.getTime() - 29 * DAY_MS)),
    to: toDateInput(today),
  };
}

const numberFormatter = new Intl.NumberFormat('en-LK');
const compactFormatter = new Intl.NumberFormat('en-LK', {
  notation: 'compact',
  maximumFractionDigits: 1,
});

export function formatNumber(value) {
  return numberFormatter.format(Number(value) || 0);
}

export function formatCompact(value) {
  return compactFormatter.format(Number(value) || 0);
}

export function formatPercent(value) {
  if (value === null || value === undefined) {
    return '—';
  }

  const number = Number(value);
  return `${number > 0 ? '+' : ''}${number.toFixed(1)}%`;
}

// Relative change between two API totals, for display next to them.
export function percentChange(current, previous) {
  if (!previous) {
    return null;
  }

  return ((current - previous) / previous) * 100;
}

const dayLabel = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' });
const monthLabel = new Intl.DateTimeFormat('en-GB', { month: 'short', year: 'numeric', timeZone: 'UTC' });

export const TREND_METRICS = {
  revenue: { label: 'Revenue', field: 'netRevenue' },
  orders: { label: 'Orders', field: 'orderCount' },
  units: { label: 'Units sold', field: 'unitsSold' },
};

// Maps /analytics/sales/over-time points to chart points for one metric.
export function toTrendPoints(response, metric = 'revenue') {
  const field = TREND_METRICS[metric].field;
  const format = response.granularity === GRANULARITY.MONTH ? monthLabel : dayLabel;

  return response.points.map((point) => ({
    key: point.periodStart,
    label: format.format(new Date(point.periodStart)),
    value: Number(point[field]) || 0,
  }));
}

// "Nice" axis ticks (0, 1,000, 2,000 …) covering [0, max].
export function niceTicks(max, count = 4) {
  if (!(max > 0)) {
    return [0, 1];
  }

  const rawStep = max / count;
  const magnitude = 10 ** Math.floor(Math.log10(rawStep));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= rawStep);
  const ticks = [];

  for (let tick = 0; tick < max + step; tick += step) {
    ticks.push(Number(tick.toFixed(10)));
    if (tick >= max) {
      break;
    }
  }

  return ticks;
}
