import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildVariantPayload,
  validateVariantForm,
} from './variantUtils';

export function VariantForm({
  colours,
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
  sizes,
}) {
  const [form, setForm] = useState(() => ({
    sku: initialValue?.sku ?? '',
    sizeId: initialValue?.sizeId ?? initialValue?.size?.id ?? '',
    colourId: initialValue?.colourId ?? initialValue?.colour?.id ?? '',
    price: initialValue?.price ?? '',
    isActive: initialValue?.isActive ?? true,
  }));
  const [validationErrors, setValidationErrors] = useState([]);

  function updateField(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function handleSubmit(event) {
    event.preventDefault();

    const errors = validateVariantForm(form, sizes, colours);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildVariantPayload(form));
  }

  return (
    <form className="entity-form" onSubmit={handleSubmit}>
      {validationErrors.length > 0 && (
        <Alert tone="danger">
          <ul className="error-list">
            {validationErrors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </Alert>
      )}

      <label>
        SKU
        <input
          maxLength={100}
          onChange={(event) => updateField('sku', event.target.value)}
          required
          value={form.sku}
        />
      </label>

      <div className="form-grid">
        <label>
          Size
          <select
            onChange={(event) => updateField('sizeId', event.target.value)}
            required
            value={form.sizeId}
          >
            <option value="">Select size</option>
            {sizes.map((size) => (
              <option key={size.id} value={size.id}>
                {size.code ? `${size.name} (${size.code})` : size.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Colour
          <select
            onChange={(event) => updateField('colourId', event.target.value)}
            required
            value={form.colourId}
          >
            <option value="">Select colour</option>
            {colours.map((colour) => (
              <option key={colour.id} value={colour.id}>
                {colour.hexCode ? `${colour.name} (${colour.hexCode})` : colour.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label>
        Price
        <input
          min="0"
          onChange={(event) => updateField('price', event.target.value)}
          required
          step="0.01"
          type="number"
          value={form.price}
        />
      </label>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active variant
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save variant'}
        </button>
        <button
          className="button-secondary"
          disabled={isSubmitting}
          onClick={onCancel}
          type="button"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
