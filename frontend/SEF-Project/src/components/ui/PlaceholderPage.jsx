import { PageShell } from './PageShell';

export function PlaceholderPage({ area, description }) {
  return (
    <PageShell
      eyebrow="Member 1 architecture"
      title={area}
      description={description}
    >
      <p className="placeholder-note">
        Route and layout are ready. Feature UI will be implemented in the next
        phases.
      </p>
    </PageShell>
  );
}
