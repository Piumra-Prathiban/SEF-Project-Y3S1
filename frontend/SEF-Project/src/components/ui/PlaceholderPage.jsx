import { PageShell } from './PageShell';

export function PlaceholderPage({ area, description }) {
  return (
    <PageShell
      eyebrow="Clothic"
      title={area}
      description={description}
    >
      <p className="placeholder-note">
        This view is planned. The route and layout are in place, and the screen
        will be added in a later release.
      </p>
    </PageShell>
  );
}
