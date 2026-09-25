import { useState } from 'react';
import FormField from '../../../components/FormField';
import { Alert } from '../../../components/StatusViews';
import { toErrorMessage, toFieldErrors } from '../../../utils/apiErrors';
import { PROMOTION_TYPE_OPTIONS, PROMOTION_TYPES } from '../marketingConstants';
import {
  EMPTY_PROMOTION_FORM,
  formToPromotionPayload,
  validatePromotionForm,
} from '../marketingUtils';

const DISCOUNT_HINTS = {
  [PROMOTION_TYPES.PERCENTAGE]: 'Percentage off, from 0.01 to 100.',
  [PROMOTION_TYPES.FIXED_AMOUNT]: 'Amount in LKR taken off each item.',
  [PROMOTION_TYPES.BUY_X_GET_Y]: 'Optional. Not used for item price discounts.',
  [PROMOTION_TYPES.FREE_SHIPPING]: 'Optional. Free shipping has no discount value.',
};

export default function PromotionForm({
  initialValues = EMPTY_PROMOTION_FORM,
  targets,
  campaigns,
  submitLabel = 'Save promotion',
  onSubmit,
  onCancel,
}) {
  const [form, setForm] = useState(initialValues);
  const [errors, setErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const selectedCampaign = campaigns.find((c) => c.id === form.campaignId);

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  function toggleId(field, id) {
    const ids = form[field].includes(id)
      ? form[field].filter((existing) => existing !== id)
      : [...form[field], id];

    update(field, ids);
    setErrors((current) => ({ ...current, productIds: undefined }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setFormError('');

    const clientErrors = validatePromotionForm(form, selectedCampaign);
    setErrors(clientErrors);

    if (Object.keys(clientErrors).length > 0) {
      setFormError('Please correct the highlighted fields.');
      return;
    }

    setSubmitting(true);

    try {
      await onSubmit(formToPromotionPayload(form));
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

        <FormField id="type" label="Promotion type" required error={errors.type}>
          {(props) => (
            <select {...props} value={form.type} onChange={(e) => update('type', e.target.value)}>
              {PROMOTION_TYPE_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          )}
        </FormField>

        <FormField
          id="discountValue"
          label="Discount value"
          hint={DISCOUNT_HINTS[Number(form.type)]}
          required={Number(form.type) <= PROMOTION_TYPES.FIXED_AMOUNT}
          error={errors.discountValue}
        >
          {(props) => (
            <input
              {...props}
              type="number"
              inputMode="decimal"
              min="0"
              step="0.01"
              value={form.discountValue}
              onChange={(e) => update('discountValue', e.target.value)}
            />
          )}
        </FormField>

        <FormField
          id="campaignId"
          label="Campaign"
          hint="Optional. The promotion's dates must fall within the campaign."
          error={errors.campaignId}
        >
          {(props) => (
            <select {...props} value={form.campaignId} onChange={(e) => update('campaignId', e.target.value)}>
              <option value="">No campaign</option>
              {campaigns.map((campaign) => (
                <option key={campaign.id} value={campaign.id}>{campaign.name}</option>
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

      <div className="field field-inline">
        <input
          id="isActive"
          type="checkbox"
          checked={form.isActive}
          onChange={(e) => update('isActive', e.target.checked)}
        />
        <label htmlFor="isActive">Active</label>
      </div>

      <div className="form-grid">
        <TargetFieldset
          legend="Products"
          field="productIds"
          options={targets.products}
          selected={form.productIds}
          onToggle={toggleId}
          describedBy={errors.productIds ? 'targets-error' : undefined}
        />
        <TargetFieldset
          legend="Categories"
          field="categoryIds"
          options={targets.categories}
          selected={form.categoryIds}
          onToggle={toggleId}
          describedBy={errors.productIds ? 'targets-error' : undefined}
        />
      </div>
      {(errors.productIds || errors.categoryIds) && (
        <p id="targets-error" className="field-error">
          {errors.productIds || errors.categoryIds}
        </p>
      )}

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

function TargetFieldset({ legend, field, options, selected, onToggle, describedBy }) {
  return (
    <fieldset className="checkbox-group" aria-describedby={describedBy}>
      <legend>{legend}</legend>
      {options.length === 0 && <p className="muted">None available.</p>}
      {options.map((option) => {
        const id = `${field}-${option.id}`;

        return (
          <div className="field-inline" key={option.id}>
            <input
              id={id}
              type="checkbox"
              checked={selected.includes(option.id)}
              onChange={() => onToggle(field, option.id)}
            />
            <label htmlFor={id}>
              {option.name}
              {!option.isActive && <span className="muted"> (inactive)</span>}
            </label>
          </div>
        );
      })}
    </fieldset>
  );
}
