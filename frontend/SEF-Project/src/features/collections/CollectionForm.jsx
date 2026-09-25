import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildCollectionPayload,
  validateCollectionForm,
} from './collectionUtils';

export function CollectionForm({
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? '',
    description: initialValue?.description ?? '',
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

    const errors = validateCollectionForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildCollectionPayload(form));
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
        Collection name
        <input
          maxLength={200}
          onChange={(event) => updateField('name', event.target.value)}
          required
          value={form.name}
        />
      </label>

      <label>
        Description
        <textarea
          maxLength={1000}
          onChange={(event) => updateField('description', event.target.value)}
          rows={4}
          value={form.description}
        />
      </label>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active collection
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save collection'}
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
