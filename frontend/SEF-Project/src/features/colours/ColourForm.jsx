import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildColourPayload,
  validateColourForm,
} from './colourUtils';

export function ColourForm({
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? '',
    hexCode: initialValue?.hexCode ?? '',
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

    const errors = validateColourForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildColourPayload(form));
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
        Colour name
        <input
          maxLength={100}
          onChange={(event) => updateField('name', event.target.value)}
          required
          value={form.name}
        />
      </label>

      <label>
        Hex code
        <input
          maxLength={7}
          onChange={(event) => updateField('hexCode', event.target.value)}
          placeholder="#000000"
          value={form.hexCode}
        />
      </label>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active colour
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save colour'}
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
