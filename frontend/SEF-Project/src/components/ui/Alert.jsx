export function Alert({ children, tone = 'info' }) {
  return (
    <div
      aria-live={tone === 'danger' ? 'assertive' : 'polite'}
      className={`alert alert--${tone}`}
      role={tone === 'danger' ? 'alert' : 'status'}
    >
      {children}
    </div>
  );
}
