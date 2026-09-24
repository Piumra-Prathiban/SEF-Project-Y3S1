import { useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import PageHeader from '../../../components/PageHeader';
import { ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import { createCampaign, getCampaign, updateCampaign } from '../../../services/campaignService';
import { campaignToForm, EMPTY_CAMPAIGN_FORM } from '../marketingUtils';
import CampaignForm from './CampaignForm';

// Handles both /marketing/campaigns/new and /marketing/campaigns/:id/edit.
export default function CampaignFormPage() {
  const { id } = useParams();
  const { token } = useAuth();
  const navigate = useNavigate();
  const isEdit = Boolean(id);

  const loader = useCallback(
    () => (id ? getCampaign(token, id) : Promise.resolve(null)),
    [token, id]
  );
  const { data, error, loading, reload } = useAsync(loader);

  const backTo = isEdit ? `/marketing/campaigns/${id}` : '/marketing/campaigns';

  async function handleSubmit(payload) {
    const saved = isEdit
      ? await updateCampaign(token, id, payload)
      : await createCampaign(token, payload);

    navigate(`/marketing/campaigns/${saved.id}`, {
      state: { flash: isEdit ? 'Campaign updated.' : 'Campaign created.' },
    });
  }

  return (
    <>
      <PageHeader
        title={isEdit ? 'Edit campaign' : 'New campaign'}
        backTo={backTo}
        backLabel={isEdit ? 'Back to campaign' : 'All campaigns'}
      />

      {loading && <LoadingState label="Loading form…" />}
      {error && <ErrorState error={error} onRetry={reload} />}
      {!loading && !error && (
        <CampaignForm
          key={id ?? 'new'}
          initialValues={data ? campaignToForm(data) : EMPTY_CAMPAIGN_FORM}
          submitLabel={isEdit ? 'Save changes' : 'Create campaign'}
          onSubmit={handleSubmit}
          onCancel={() => navigate(backTo)}
        />
      )}
    </>
  );
}
