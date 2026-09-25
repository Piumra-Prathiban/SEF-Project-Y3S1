export function PageShell({ title, eyebrow, description, children }) {
  return (
    <section className="page-shell">
      {eyebrow && <p className="page-shell__eyebrow">{eyebrow}</p>}
      <h1>{title}</h1>
      {description && <p className="page-shell__description">{description}</p>}
      {children && <div className="page-shell__content">{children}</div>}
    </section>
  );
}
