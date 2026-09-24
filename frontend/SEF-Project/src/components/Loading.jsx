import './Loading.css';

function Loading() {
  return (
    <p className="loading" role="status">
      <span className="loading__spinner" aria-hidden="true" />
      Loading…
    </p>
  );
}

export default Loading;
