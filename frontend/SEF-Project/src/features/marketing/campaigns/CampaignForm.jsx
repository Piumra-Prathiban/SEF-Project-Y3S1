import { useState } from 'react';
import FormField from '../../../components/FormField';
import { Alert } from '../../../components/StatusViews';
import { toErrorMessage, toFieldErrors } from '../../../utils/apiErrors';
import { CAMPAIGN_STATUS_OPTIONS, CLOSED_CAMPAIGN_STATUSES } from '../marketingConstants';
import {
  EMPTY_CAMPAIGN_FORM,
  formToCampaignPayload,
  validateCampaignForm,
} from '../marketingUtils';

export default function CampaignForm({
  initialValues = EMPTY_CAMPAIGN_FORM,
  submitLabel = 'Save campaign',
  onSubmit,
  onCancel,
}) {
  const [form, setForm] = useState(initialValues);
  const [errors, setErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  // A completed/cancelled campaign cannot change status (enforced by the API).
  const statusLocked = CLOSED_CAMPAIGN_STATUSES.includes(Number(initialValues.status));

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setFormError('');

    const clientErrors = validateCampaignForm(form);
    setErrors(clientErrors);

    if (Object.keys(clientErrors).length > 0) {
      setFormError('Please correct the highlighted fields.');
      return;
    }

    setSubmitting(true);

    try {
      await onSubmit(formToCampaignPayload(form));
    } catch (error) {
      setErrors(toFieldErrors(error));
      setFormError(toErrorMessage(error));
      setSubmitting(false);
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      <Alert tone="danger">{formError}</Alert>

      <div className="form-grid">
        <FormField id="name" label="Name" required error={errors.name}>
          {(props) => (
            <input
              {...props}
              type="text"
              maxLength={200}
              value={form.name}
              onChange={(e) => update('name', e.target.value)}
            />
          )}
        </FormField>

        <FormField
          id="status"
          label="Status"
          required
          error={errors.status}
          hint={statusLocked ? 'Completed and cancelled campaigns cannot change status.' : undefined}
        >
          {(props) => (
            <select
              {...props}
              value={form.status}
              disabled={statusLocked}
              onChange={(e) => update('status', e.target.value)}
            >
              {CAMPAIGN_STATUS_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          )}
        </FormField>

        <FormField id="startDate" label="Start date (UTC)" required error={errors.startDate}>
          {(props) => (
            <input
              {...props}
              type="date"
              value={form.startDate}
              onChange={(e) => update('startDate', e.target.value)}
            />
          )}
        </FormField>

        <FormField id="endDate" label="End date (UTC)" required error={errors.endDate}>
          {(props) => (
            <input
              {...props}
              type="date"
              min={form.startDate || undefined}
              value={form.endDate}
              onChange={(e) => update('endDate', e.target.value)}
            />
          )}
        </FormField>
      </div>

      <FormField id="description" label="Description" error={errors.description}>
        {(props) => (
          <textarea
            {...props}
            rows={3}
            maxLength={2000}
            value={form.description}
            onChange={(e) => update('description', e.target.value)}
          />
        )}
      </FormField>

      <div className="button-row form-actions">
        <button type="submit" className="button" disabled={submitting}>
          {submitting ? 'Saving…' : submitLabel}
        </button>
        {onCancel && (
          <button type="button" className="button button-secondary" onClick={onCancel} disabled={submitting}>
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
