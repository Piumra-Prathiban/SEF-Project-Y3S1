import { useCallback } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import PageHeader from '../../../components/PageHeader';
import { ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import { getCampaigns } from '../../../services/campaignService';
import {
  createPromotion,
  getPromotion,
  getPromotionTargets,
  updatePromotion,
} from '../../../services/promotionService';
import { EMPTY_PROMOTION_FORM, promotionToForm } from '../marketingUtils';
import PromotionForm from './PromotionForm';

// Handles both /marketing/promotions/new and /marketing/promotions/:id/edit.
export default function PromotionFormPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const presetCampaignId = searchParams.get('campaignId') ?? '';
  const { token } = useAuth();
  const navigate = useNavigate();
  const isEdit = Boolean(id);

  const loader = useCallback(async () => {
    const [targets, campaigns, promotion] = await Promise.all([
      getPromotionTargets(token),
      getCampaigns(token, { pageSize: 100, sortBy: 'name', sortDirection: 'asc' }),
      id ? getPromotion(token, id) : Promise.resolve(null),
    ]);
    return { targets, campaigns: campaigns.items, promotion };
  }, [token, id]);

  const { data, error, loading, reload } = useAsync(loader);

  const backTo = isEdit ? `/marketing/promotions/${id}` : '/marketing/promotions';

  async function handleSubmit(payload) {
    const saved = isEdit
      ? await updatePromotion(token, id, payload)
      : await createPromotion(token, payload);

    navigate(`/marketing/promotions/${saved.id}`, {
      state: { flash: isEdit ? 'Promotion updated.' : 'Promotion created.' },
    });
  }

  return (
    <>
      <PageHeader
        title={isEdit ? 'Edit promotion' : 'New promotion'}
        backTo={backTo}
        backLabel={isEdit ? 'Back to promotion' : 'All promotions'}
      />

      {loading && <LoadingState label="Loading form…" />}
      {error && <ErrorState error={error} onRetry={reload} />}
      {data && (
        <PromotionForm
          key={id ?? 'new'}
          initialValues={
            data.promotion
              ? promotionToForm(data.promotion)
              : { ...EMPTY_PROMOTION_FORM, campaignId: presetCampaignId }
          }
          targets={data.targets}
          campaigns={data.campaigns}
          submitLabel={isEdit ? 'Save changes' : 'Create promotion'}
          onSubmit={handleSubmit}
          onCancel={() => navigate(backTo)}
        />
      )}
    </>
  );
}
