import { useCallback, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import Badge from '../../../components/Badge';
import ConfirmAction from '../../../components/ConfirmAction';
import PageHeader from '../../../components/PageHeader';
import { Alert, EmptyState, ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import { deleteCampaign, getCampaign, updateCampaign } from '../../../services/campaignService';
import { getPromotions } from '../../../services/promotionService';
import { toErrorMessage } from '../../../utils/apiErrors';
import { CAMPAIGN_STATUS_OPTIONS, CLOSED_CAMPAIGN_STATUSES } from '../marketingConstants';
import {
  campaignStatusLabel,
  campaignStatusTone,
  campaignToForm,
  formatDate,
  formatDiscount,
  formToCampaignPayload,
  promotionState,
} from '../marketingUtils';

export default function CampaignDetailPage() {
  const { id } = useParams();
  const { token } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState({ tone: 'success', text: '' });
  const [newStatus, setNewStatus] = useState('');

  const loader = useCallback(async () => {
    const [campaign, promotions] = await Promise.all([
      getCampaign(token, id),
      getPromotions(token, { campaignId: id, pageSize: 100, sortBy: 'startDate', sortDirection: 'asc' }),
    ]);
    return { campaign, promotions: promotions.items };
  }, [token, id]);

  const { data, error, loading, reload } = useAsync(loader);

  if (loading) {
    return <LoadingState label="Loading campaign…" />;
  }

  if (error) {
    return <ErrorState error={error} onRetry={reload} />;
  }

  const { campaign, promotions } = data;
  const statusLocked = CLOSED_CAMPAIGN_STATUSES.includes(campaign.status);
  const selectedStatus = newStatus === '' ? String(campaign.status) : newStatus;

  async function handleStatusChange(event) {
    event.preventDefault();
    setBusy(true);
    setMessage({ tone: 'success', text: '' });

    try {
      await updateCampaign(token, campaign.id, {
        ...formToCampaignPayload(campaignToForm(campaign)),
        status: Number(selectedStatus),
      });
      setMessage({
        tone: 'success',
        text: `Status changed to ${campaignStatusLabel(Number(selectedStatus))}.`,
      });
      setNewStatus('');
      reload();
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    setBusy(true);

    try {
      await deleteCampaign(token, campaign.id);
      navigate('/marketing/campaigns', {
        state: { flash: `Campaign "${campaign.name}" deleted.` },
      });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader
        title={campaign.name}
        backTo="/marketing/campaigns"
        backLabel="All campaigns"
        actions={
          <>
            <Link className="button" to={`/marketing/campaigns/${campaign.id}/edit`}>Edit</Link>
            <ConfirmAction
              label="Delete"
              question="Delete this campaign?"
              confirmLabel="Yes, delete"
              onConfirm={handleDelete}
              busy={busy}
            />
          </>
        }
      />

      <Alert tone={message.tone} onDismiss={() => setMessage({ tone: 'success', text: '' })}>
        {message.text}
      </Alert>

      <dl className="details">
        <dt>Status</dt>
        <dd>
          <Badge tone={campaignStatusTone(campaign.status)}>
            {campaignStatusLabel(campaign.status)}
          </Badge>
        </dd>
        <dt>Dates (UTC)</dt>
        <dd>{formatDate(campaign.startDate)} – {formatDate(campaign.endDate)}</dd>
        <dt>Description</dt>
        <dd>{campaign.description || '—'}</dd>
        <dt>Last updated</dt>
        <dd>{formatDate(campaign.updatedAt)}</dd>
      </dl>

      <form className="inline-form" onSubmit={handleStatusChange} aria-label="Change campaign status">
        <div className="field">
          <label htmlFor="campaign-status">Change status</label>
          <select
            id="campaign-status"
            value={selectedStatus}
            disabled={statusLocked || busy}
            onChange={(e) => setNewStatus(e.target.value)}
            aria-describedby={statusLocked ? 'campaign-status-hint' : undefined}
          >
            {CAMPAIGN_STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
          {statusLocked && (
            <p id="campaign-status-hint" className="field-hint">
              Completed and cancelled campaigns cannot change status.
            </p>
          )}
        </div>
        <button
          type="submit"
          className="button button-secondary"
          disabled={statusLocked || busy || selectedStatus === String(campaign.status)}
        >
          Update status
        </button>
      </form>

      <section aria-labelledby="campaign-promotions">
        <div className="section-heading">
          <h2 id="campaign-promotions">Promotions ({promotions.length})</h2>
          {!statusLocked && (
            <Link
              className="button button-secondary button-small"
              to={`/marketing/promotions/new?campaignId=${campaign.id}`}
            >
              Add promotion
            </Link>
          )}
        </div>

        {promotions.length === 0 ? (
          <EmptyState title="This campaign has no promotions yet." />
        ) : (
          <ul className="card-list">
            {promotions.map((promotion) => {
              const state = promotionState(promotion);

              return (
                <li key={promotion.id} className="card">
                  <Link to={`/marketing/promotions/${promotion.id}`}>{promotion.name}</Link>
                  <span>{formatDiscount(promotion)}</span>
                  <span>{formatDate(promotion.startDate)} – {formatDate(promotion.endDate)}</span>
                  <Badge tone={state.tone}>{state.label}</Badge>
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </>
  );
}
