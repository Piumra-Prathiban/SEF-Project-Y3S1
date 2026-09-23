export function LoadingState({ message = 'Loading...' }) {
  return (
    <div className="loading-state" aria-live="polite">
      {message}
    </div>
  );
}
