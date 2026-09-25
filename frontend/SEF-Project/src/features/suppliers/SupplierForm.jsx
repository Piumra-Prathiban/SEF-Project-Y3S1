import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildSupplierPayload,
  validateSupplierForm,
} from './supplierUtils';

export function SupplierForm({
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? '',
    contactName: initialValue?.contactName ?? '',
    email: initialValue?.email ?? '',
    phone: initialValue?.phone ?? '',
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

    const errors = validateSupplierForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildSupplierPayload(form));
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
        Supplier name
        <input
          maxLength={200}
          onChange={(event) => updateField('name', event.target.value)}
          required
          value={form.name}
        />
      </label>

      <label>
        Contact person
        <input
          maxLength={200}
          onChange={(event) => updateField('contactName', event.target.value)}
          value={form.contactName}
        />
      </label>

      <label>
        Email
        <input
          maxLength={320}
          onChange={(event) => updateField('email', event.target.value)}
          type="email"
          value={form.email}
        />
      </label>

      <label>
        Phone
        <input
          maxLength={50}
          onChange={(event) => updateField('phone', event.target.value)}
          value={form.phone}
        />
      </label>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active supplier
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save supplier'}
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
