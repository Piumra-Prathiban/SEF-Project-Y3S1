import { useState } from 'react';
import { customRangeDefaults, describeRange, RANGE_PRESETS } from './analyticsUtils';

// One filter row above the content it scopes: date range first, then refresh.
export default function DateRangeFilter({ selection, apiRange, onChange, onRefresh, refreshing }) {
  const [custom, setCustom] = useState(() =>
    selection.from && selection.to
      ? { from: selection.from, to: selection.to }
      : customRangeDefaults()
  );
  const [error, setError] = useState('');

  function handlePreset(range) {
    setError('');

    if (range === 'custom') {
      onChange({ range, from: custom.from, to: custom.to });
    } else {
      onChange({ range, from: '', to: '' });
    }
  }

  function handleApply(event) {
    event.preventDefault();

    if (!custom.from || !custom.to) {
      setError('Choose both dates.');
      return;
    }

    if (custom.to < custom.from) {
      setError('The end date must be on or after the start date.');
      return;
    }

    setError('');
    onChange({ range: 'custom', from: custom.from, to: custom.to });
  }

  return (
    <div className="filter-row" role="group" aria-label="Report period">
      <div className="field">
        <label htmlFor="date-range">Period</label>
        <select id="date-range" value={selection.range} onChange={(e) => handlePreset(e.target.value)}>
          {RANGE_PRESETS.map((preset) => (
            <option key={preset.value} value={preset.value}>{preset.label}</option>
          ))}
        </select>
      </div>

      {selection.range === 'custom' && (
        <form className="custom-range" onSubmit={handleApply} noValidate>
          <div className="field">
            <label htmlFor="range-from">From</label>
            <input
              id="range-from"
              type="date"
              value={custom.from}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? 'range-error' : undefined}
              onChange={(e) => setCustom((c) => ({ ...c, from: e.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="range-to">To</label>
            <input
              id="range-to"
              type="date"
              value={custom.to}
              min={custom.from || undefined}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? 'range-error' : undefined}
              onChange={(e) => setCustom((c) => ({ ...c, to: e.target.value }))}
            />
          </div>
          <button type="submit" className="button button-secondary">Apply</button>
          {error && <p id="range-error" className="field-error">{error}</p>}
        </form>
      )}

      <p className="range-summary muted" aria-live="polite">
        {describeRange(apiRange)} (UTC)
      </p>

      {onRefresh && (
        <button
          type="button"
          className="button button-secondary"
          onClick={onRefresh}
          disabled={refreshing}
        >
          {refreshing ? 'Refreshing…' : 'Refresh'}
        </button>
      )}
    </div>
  );
}
