import { Link } from 'react-router-dom';
import { toErrorMessage } from '../utils/apiErrors';

export function LoadingState({ label = 'Loading…' }) {
  return (
    <div className="state state-loading" role="status" aria-live="polite">
      <span className="spinner" aria-hidden="true" />
      {label}
    </div>
  );
}

export function EmptyState({ title, children, action }) {
  return (
    <div className="state state-empty">
      <p className="state-title">{title}</p>
      {children && <p>{children}</p>}
      {action}
    </div>
  );
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="state state-error" role="alert">
      <p className="state-title">{toErrorMessage(error)}</p>
      <div className="button-row">
        {error?.status === 401 ? (
          <Link className="button" to="/login">Log in</Link>
        ) : (
          onRetry && (
            <button type="button" className="button" onClick={onRetry}>
              Try again
            </button>
          )
        )}
      </div>
    </div>
  );
}

export function Alert({ tone = 'info', children, onDismiss }) {
  if (!children) {
    return null;
  }

  return (
    <div
      className={`alert alert-${tone}`}
      role={tone === 'danger' ? 'alert' : 'status'}
    >
      <span>{children}</span>
      {onDismiss && (
        <button
          type="button"
          className="alert-dismiss"
          onClick={onDismiss}
          aria-label="Dismiss message"
        >
          ×
        </button>
      )}
    </div>
  );
}
