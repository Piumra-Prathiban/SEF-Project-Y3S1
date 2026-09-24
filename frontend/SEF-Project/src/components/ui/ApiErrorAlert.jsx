import { Alert } from './Alert';

export function ApiErrorAlert({ message, onRetry, retryLabel = 'Try again' }) {
  if (!message) {
    return null;
  }

  return (
    <Alert tone="danger">
      <div className="feedback-row">
        <span>{message}</span>
        {onRetry && (
          <button
            className="button-secondary"
            onClick={onRetry}
            type="button"
          >
            {retryLabel}
          </button>
        )}
      </div>
    </Alert>
  );
}
