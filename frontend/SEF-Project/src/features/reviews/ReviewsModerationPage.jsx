import { PageShell } from '../../components/ui/PageShell';
import { ReviewsModerationSection } from './ReviewsModerationSection';

/** Staff page that hosts the review moderation table. */
export function ReviewsModerationPage() {
  return (
    <PageShell
      description="Hide a review from the storefront, or remove it permanently. Filters help you focus on a single product or rating band."
      eyebrow="Clothic · Catalog"
      title="Reviews"
    >
      <ReviewsModerationSection />
    </PageShell>
  );
}

export default ReviewsModerationPage;
