import './ErrorAlert.css';

function ErrorAlert({ error, onRetry }) {
  return (
    <div className="error-alert" role="alert">
      <p className="error-alert__message">
        {error?.message || 'Something went wrong. Please try again.'}
      </p>
      {onRetry && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  );
}

export default ErrorAlert;
