import { Link } from 'react-router-dom';

export default function PageHeader({ title, subtitle, backTo, backLabel, actions }) {
  return (
    <header className="page-header">
      <div>
        {backTo && (
          <Link className="back-link" to={backTo}>
            ← {backLabel ?? 'Back'}
          </Link>
        )}
        <h1>{title}</h1>
        {subtitle && <p className="page-subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="button-row">{actions}</div>}
    </header>
  );
}
