import { useLayoutEffect, useRef, useState } from 'react';
import { niceTicks } from '../../features/marketing/analytics/analyticsUtils';

const HEIGHT = 240;
const MARGIN = { top: 16, right: 20, bottom: 30, left: 64 };

function useElementWidth(fallback) {
  const ref = useRef(null);
  const [width, setWidth] = useState(fallback);

  useLayoutEffect(() => {
    const element = ref.current;

    if (!element || typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    const observer = new ResizeObserver(([entry]) => {
      setWidth(Math.max(280, Math.round(entry.contentRect.width)));
    });
    observer.observe(element);

    return () => observer.disconnect();
  }, []);

  return [ref, width];
}

/**
 * Single-series line chart with a 10% area wash, crosshair tooltip (pointer
 * and arrow keys) and a data-table view. `points`: [{ key, label, value }].
 */
export default function LineChart({ points, title, formatValue = String, formatTick = String }) {
  const [containerRef, width] = useElementWidth(640);
  const [activeIndex, setActiveIndex] = useState(null);

  const plotWidth = width - MARGIN.left - MARGIN.right;
  const plotHeight = HEIGHT - MARGIN.top - MARGIN.bottom;
  const maxValue = Math.max(0, ...points.map((p) => p.value));
  const ticks = niceTicks(maxValue);
  const yMax = ticks[ticks.length - 1];
  const step = points.length > 1 ? plotWidth / (points.length - 1) : 0;

  const x = (index) => MARGIN.left + (points.length > 1 ? index * step : plotWidth / 2);
  const y = (value) => MARGIN.top + plotHeight - (value / yMax) * plotHeight;

  const linePath = points
    .map((p, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(p.value).toFixed(1)}`)
    .join(' ');
  const baseline = MARGIN.top + plotHeight;
  const areaPath = points.length > 1
    ? `${linePath} L${x(points.length - 1).toFixed(1)},${baseline} L${x(0).toFixed(1)},${baseline} Z`
    : '';

  const labelEvery = Math.max(1, Math.ceil(points.length / Math.max(2, Math.floor(plotWidth / 90))));
  const last = points.length - 1;
  const active = activeIndex === null ? null : points[activeIndex];

  function indexFromPointer(event) {
    const bounds = event.currentTarget.getBoundingClientRect();
    const scale = width / bounds.width;
    const pointerX = (event.clientX - bounds.left) * scale;
    const index = step ? Math.round((pointerX - MARGIN.left) / step) : 0;
    return Math.min(last, Math.max(0, index));
  }

  function handleKeyDown(event) {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault();
      const delta = event.key === 'ArrowRight' ? 1 : -1;
      setActiveIndex((current) => Math.min(last, Math.max(0, (current ?? last) + delta)));
    } else if (event.key === 'Escape') {
      setActiveIndex(null);
    }
  }

  return (
    <figure className="chart">
      <div className="chart-plot" ref={containerRef}>
        <svg
          className="chart-svg"
          width="100%"
          viewBox={`0 0 ${width} ${HEIGHT}`}
          role="img"
          aria-label={`${title}. Use the left and right arrow keys to read values.`}
          tabIndex={0}
          onPointerMove={(event) => setActiveIndex(indexFromPointer(event))}
          onPointerLeave={() => setActiveIndex(null)}
          onFocus={() => setActiveIndex((current) => current ?? last)}
          onBlur={() => setActiveIndex(null)}
          onKeyDown={handleKeyDown}
        >
          {ticks.map((tick) => (
            <g key={tick}>
              <line
                className={tick === 0 ? 'chart-baseline' : 'chart-grid'}
                x1={MARGIN.left}
                x2={width - MARGIN.right}
                y1={y(tick)}
                y2={y(tick)}
              />
              <text className="chart-axis" x={MARGIN.left - 8} y={y(tick)} dy="0.32em" textAnchor="end">
                {formatTick(tick)}
              </text>
            </g>
          ))}

          {points.map((point, index) =>
            index % labelEvery === 0 || index === last ? (
              <text
                key={point.key}
                className="chart-axis"
                x={x(index)}
                y={HEIGHT - 8}
                textAnchor={index === 0 && points.length > 1 ? 'start' : index === last && points.length > 1 ? 'end' : 'middle'}
              >
                {point.label}
              </text>
            ) : null
          )}

          {areaPath && <path className="chart-area" d={areaPath} />}
          {points.length > 1 && <path className="chart-line" d={linePath} />}

          {points.length > 0 && (
            <>
              <circle className="chart-marker" cx={x(last)} cy={y(points[last].value)} r={4} />
              <text
                className="chart-end-label"
                x={x(last)}
                y={y(points[last].value) - 10}
                textAnchor={points.length > 1 ? 'end' : 'middle'}
              >
                {formatValue(points[last].value)}
              </text>
            </>
          )}

          {active && (
            <g aria-hidden="true">
              <line className="chart-crosshair" x1={x(activeIndex)} x2={x(activeIndex)} y1={MARGIN.top} y2={baseline} />
              <circle className="chart-marker" cx={x(activeIndex)} cy={y(active.value)} r={4} />
            </g>
          )}
        </svg>

        {active && (
          <div
            className="chart-tooltip"
            style={{
              left: `${(x(activeIndex) / width) * 100}%`,
              transform: activeIndex > points.length / 2 ? 'translateX(calc(-100% - 12px))' : 'translateX(12px)',
            }}
            role="status"
          >
            <strong>{formatValue(active.value)}</strong>
            <span>{active.label}</span>
          </div>
        )}
      </div>

      <details className="chart-table">
        <summary>Show data table</summary>
        <div className="table-wrap">
          <table>
            <caption className="visually-hidden">{title}</caption>
            <thead>
              <tr>
                <th scope="col">Period</th>
                <th scope="col" className="numeric">Value</th>
              </tr>
            </thead>
            <tbody>
              {points.map((point) => (
                <tr key={point.key}>
                  <td>{point.label}</td>
                  <td className="numeric">{formatValue(point.value)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </figure>
  );
}
