import { Link } from 'react-router-dom';

/**
 * Horizontal single-series bars with the value at the tip.
 * `items`: [{ key, label, value, valueLabel, to? }]. Bars share one baseline
 * and scale; the text is always in ink, never the bar colour.
 */
export default function BarList({ items, title }) {
  const max = Math.max(0, ...items.map((item) => item.value));

  return (
    <ol className="bar-list" aria-label={title}>
      {items.map((item) => {
        const percent = max > 0 ? (item.value / max) * 100 : 0;

        return (
          <li key={item.key} className="bar-row">
            <span className="bar-label">
              {item.to ? <Link to={item.to}>{item.label}</Link> : item.label}
            </span>
            <span className="bar-track" aria-hidden="true">
              <span
                className="bar-fill"
                data-testid="bar-fill"
                style={{ width: `${percent}%` }}
              />
            </span>
            <span className="bar-value">{item.valueLabel ?? item.value}</span>
          </li>
        );
      })}
    </ol>
  );
}
