import { formatPercent } from '../../features/marketing/analytics/analyticsUtils';

// Stat tile: label · value · optional change vs the previous period.
// Direction is shown with an arrow and sign, not colour alone.
export default function KpiCard({ label, value, change, changeLabel = 'vs previous period' }) {
  const hasChange = change !== null && change !== undefined;
  const direction = !hasChange || change === 0 ? 'flat' : change > 0 ? 'up' : 'down';
  const arrow = { up: '▲', down: '▼', flat: '■' }[direction];

  return (
    <div className="kpi-card">
      <span className="kpi-label">{label}</span>
      <span className="kpi-value">{value}</span>
      {hasChange ? (
        <span className={`kpi-change kpi-change-${direction}`}>
          <span aria-hidden="true">{arrow} </span>
          {formatPercent(change)} <span className="muted">{changeLabel}</span>
        </span>
      ) : (
        <span className="kpi-change muted">No previous-period data</span>
      )}
    </div>
  );
}
