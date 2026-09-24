import { EmptyState, ErrorState, LoadingState } from '../StatusViews';

/**
 * A titled dashboard card that handles loading / error / empty states.
 * While refetching it keeps the previous render at reduced opacity.
 */
export default function DashboardSection({
  id,
  title,
  description,
  controls,
  loading,
  error,
  onRetry,
  hasData,
  isEmpty,
  emptyTitle = 'No data for this period.',
  emptyHint,
  children,
}) {
  let body;

  if (error) {
    body = <ErrorState error={error} onRetry={onRetry} />;
  } else if (!hasData) {
    body = <LoadingState label={`Loading ${title.toLowerCase()}…`} />;
  } else if (isEmpty) {
    body = <EmptyState title={emptyTitle}>{emptyHint}</EmptyState>;
  } else {
    body = (
      <div className={loading ? 'section-body is-refreshing' : 'section-body'} aria-busy={loading}>
        {children}
      </div>
    );
  }

  return (
    <section className="dashboard-section" aria-labelledby={id}>
      <header className="dashboard-section-header">
        <div>
          <h2 id={id}>{title}</h2>
          {description && <p className="muted">{description}</p>}
        </div>
        {controls && <div className="section-controls">{controls}</div>}
      </header>
      {body}
    </section>
  );
}
