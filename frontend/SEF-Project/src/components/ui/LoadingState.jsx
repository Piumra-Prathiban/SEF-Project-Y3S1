export function LoadingState({ message = 'Loading...' }) {
  return (
    <div className="loading-state" aria-live="polite" role="status">
      {message}
    </div>
  );
}
