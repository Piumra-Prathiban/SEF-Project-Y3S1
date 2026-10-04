import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import FormField from '../../components/FormField';
import PageHeader from '../../components/PageHeader';
import { Alert } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { useAsync } from '../../hooks/useAsync';
import { OrderStatusName, getOrders } from '../../services/orderService';
import { startFulfilmentAgentWorkflow } from '../../services/fulfilmentAgentService';
import { toErrorMessage } from '../../utils/apiErrors';
import { buildStartPayload, EMPTY_START_FORM, validateStartForm } from './fulfilmentAgentUtils';

export default function FulfilmentAgentStartPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(EMPTY_START_FORM);
  const [errors, setErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const ordersLoader = useCallback(
    () => getOrders(token, { pageSize: 100, sortBy: 'createdAt', sortDirection: 'desc' }),
    [token],
  );
  const {
    data: ordersData,
    error: ordersError,
    loading: ordersLoading,
  } = useAsync(ordersLoader);

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setFormError('');

    const clientErrors = validateStartForm(form);
    setErrors(clientErrors);

    if (Object.keys(clientErrors).length > 0) {
      setFormError('Please correct the highlighted fields.');
      return;
    }

    setSubmitting(true);

    try {
      const workflow = await startFulfilmentAgentWorkflow(token, buildStartPayload(form));
      navigate(`/fulfilment-agent/${workflow.workflowId}`, {
        state: { flash: 'Workflow started.' },
      });
    } catch (error) {
      setFormError(toErrorMessage(error));
      setSubmitting(false);
    }
  }

  return (
    <>
      <PageHeader
        title="Start a fulfilment agent run"
        backTo="/fulfilment-agent"
        backLabel="All workflows"
      />

      <form className="form" onSubmit={handleSubmit} noValidate>
        {formError && <Alert tone="danger">{formError}</Alert>}
        {ordersError && <Alert tone="danger">Could not load orders: {toErrorMessage(ordersError)}</Alert>}

        <FormField id="objective" label="Objective" required error={errors.objective}>
          {(props) => (
            <textarea
              {...props}
              rows={2}
              maxLength={500}
              value={form.objective}
              onChange={(e) => update('objective', e.target.value)}
            />
          )}
        </FormField>

        <FormField
          id="orderId"
          label="Order"
          required
          error={errors.orderId}
          hint="The agent investigates the selected order and proposes its next permitted action."
        >
          {(props) => (
            <select
              {...props}
              value={form.orderId}
              disabled={ordersLoading}
              onChange={(e) => update('orderId', e.target.value)}
            >
              <option value="">
                {ordersLoading ? 'Loading orders…' : 'Select an order…'}
              </option>
              {(ordersData?.items ?? []).map((order) => (
                <option key={order.id} value={order.id}>
                  {order.orderNumber} — {OrderStatusName[order.status] ?? 'Unknown'}
                </option>
              ))}
            </select>
          )}
        </FormField>

        {!ordersLoading && !ordersError && (ordersData?.items ?? []).length === 0 && (
          <p className="muted">No orders found. Create an order before starting a run.</p>
        )}

        <div className="button-row form-actions">
          <button type="submit" className="button" disabled={submitting || ordersLoading}>
            {submitting ? 'Starting…' : 'Start run'}
          </button>
        </div>
      </form>
    </>
  );
}
