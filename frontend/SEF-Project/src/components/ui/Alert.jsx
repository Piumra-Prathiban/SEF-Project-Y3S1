export function Alert({ children, tone = 'info' }) {
  return (
    <div className={`alert alert--${tone}`} role="alert">
      {children}
    </div>
  );
}
