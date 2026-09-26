import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildSizePayload,
  validateSizeForm,
} from './sizeUtils';

export function SizeForm({
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? '',
    description: initialValue?.description ?? '',
    displayOrder: initialValue?.displayOrder ?? 0,
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

    const errors = validateSizeForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildSizePayload(form));
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
        Size name
        <input
          maxLength={100}
          onChange={(event) => updateField('name', event.target.value)}
          required
          value={form.name}
        />
      </label>

      <label>
        Description
        <textarea maxLength={500} onChange={(event) => updateField('description', event.target.value)} rows={3} value={form.description} />
      </label>

      <label>
        Display order
        <input min="0" onChange={(event) => updateField('displayOrder', event.target.value)} required step="1" type="number" value={form.displayOrder} />
        <small>Lower numbers appear first in size lists.</small>
      </label>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active size
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save size'}
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
