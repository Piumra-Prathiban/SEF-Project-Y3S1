export function LoadingState({ message = 'Loading…' }) {
  return (
    <div className="state-panel" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{message}</p>
    </div>
  );
}

export function EmptyState({ title, message, action }) {
  return (
    <div className="state-panel state-panel--empty">
      <span className="state-icon" aria-hidden="true">◇</span>
      <h2>{title}</h2>
      <p>{message}</p>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }) {
  return (
    <div className="state-panel state-panel--error" role="alert">
      <h2>Something went wrong</h2>
      <p>{message}</p>
      {onRetry && (
        <button type="button" className="button button--secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}
