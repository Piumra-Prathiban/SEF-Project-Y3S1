import { useCallback, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import Badge from '../../../components/Badge';
import ConfirmAction from '../../../components/ConfirmAction';
import PageHeader from '../../../components/PageHeader';
import { Alert, ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import {
  deletePromotion,
  getPromotion,
  getPromotionTargets,
  updatePromotion,
} from '../../../services/promotionService';
import { toErrorMessage } from '../../../utils/apiErrors';
import {
  formatDate,
  formatDiscount,
  promotionState,
  promotionTypeLabel,
  withActiveState,
} from '../marketingUtils';

export default function PromotionDetailPage() {
  const { id } = useParams();
  const { token } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState({ tone: 'success', text: '' });

  const loader = useCallback(async () => {
    const [promotion, targets] = await Promise.all([
      getPromotion(token, id),
      getPromotionTargets(token),
    ]);
    return { promotion, targets };
  }, [token, id]);

  const { data, error, loading, reload } = useAsync(loader);

  if (loading) {
    return <LoadingState label="Loading promotion…" />;
  }

  if (error) {
    return <ErrorState error={error} onRetry={reload} />;
  }

  const { promotion, targets } = data;
  const state = promotionState(promotion);
  const nameOf = (options, targetId) =>
    options.find((o) => o.id === targetId)?.name ?? targetId;

  async function handleToggleActive() {
    setBusy(true);
    setMessage({ tone: 'success', text: '' });

    try {
      await updatePromotion(token, promotion.id, withActiveState(promotion, !promotion.isActive));
      setMessage({
        tone: 'success',
        text: promotion.isActive ? 'Promotion deactivated.' : 'Promotion activated.',
      });
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
      await deletePromotion(token, promotion.id);
      navigate('/marketing/promotions', {
        state: { flash: `Promotion "${promotion.name}" deleted.` },
      });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader
        title={promotion.name}
        backTo="/marketing/promotions"
        backLabel="All promotions"
        actions={
          <>
            <Link className="button" to={`/marketing/promotions/${promotion.id}/edit`}>Edit</Link>
            <button
              type="button"
              className="button button-secondary"
              onClick={handleToggleActive}
              disabled={busy}
            >
              {promotion.isActive ? 'Deactivate' : 'Activate'}
            </button>
            <ConfirmAction
              label="Delete"
              question="Delete this promotion?"
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
        <dd><Badge tone={state.tone}>{state.label}</Badge></dd>
        <dt>Type</dt>
        <dd>{promotionTypeLabel(promotion.type)}</dd>
        <dt>Discount</dt>
        <dd>{formatDiscount(promotion)}</dd>
        <dt>Dates (UTC)</dt>
        <dd>{formatDate(promotion.startDate)} – {formatDate(promotion.endDate)}</dd>
        <dt>Campaign</dt>
        <dd>
          {promotion.campaignId ? (
            <Link to={`/marketing/campaigns/${promotion.campaignId}`}>{promotion.campaignName}</Link>
          ) : (
            '—'
          )}
        </dd>
        <dt>Description</dt>
        <dd>{promotion.description || '—'}</dd>
        <dt>Products</dt>
        <dd>
          {promotion.productIds.length === 0
            ? '—'
            : promotion.productIds.map((pid) => nameOf(targets.products, pid)).join(', ')}
        </dd>
        <dt>Categories</dt>
        <dd>
          {promotion.categoryIds.length === 0
            ? '—'
            : promotion.categoryIds.map((cid) => nameOf(targets.categories, cid)).join(', ')}
        </dd>
        <dt>Last updated</dt>
        <dd>{formatDate(promotion.updatedAt)}</dd>
      </dl>
    </>
  );
}
