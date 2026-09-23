import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';

const initialFormState = {
  name: '',
  description: '',
  categoryId: '',
  collectionId: '',
  supplierId: '',
  isActive: true,
};

export function ProductForm({
  categories,
  collections,
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? initialFormState.name,
    description: initialValue?.description ?? initialFormState.description,
    categoryId: initialValue?.categoryId ?? initialFormState.categoryId,
    collectionId: initialValue?.collectionId ?? initialFormState.collectionId,
    supplierId: initialValue?.supplierId ?? initialFormState.supplierId,
    isActive: initialValue?.isActive ?? initialFormState.isActive,
  }));
  const [validationErrors, setValidationErrors] = useState([]);

  function updateField(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function validate() {
    const errors = [];

    if (!form.name.trim()) {
      errors.push('Product name is required.');
    }

    if (!form.categoryId) {
      errors.push('Category is required.');
    }

    if (!form.collectionId) {
      errors.push('Collection is required.');
    }

    return errors;
  }

  function handleSubmit(event) {
    event.preventDefault();

    const errors = validate();
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit({
      name: form.name.trim(),
      description: form.description.trim() || null,
      categoryId: form.categoryId,
      collectionId: form.collectionId,
      supplierId: form.supplierId || null,
      isActive: form.isActive,
    });
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
        Product name
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
          maxLength={2000}
          onChange={(event) => updateField('description', event.target.value)}
          rows={4}
          value={form.description}
        />
      </label>

      <div className="form-grid">
        <label>
          Category
          <select
            onChange={(event) => updateField('categoryId', event.target.value)}
            required
            value={form.categoryId}
          >
            <option value="">Select category</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Collection
          <select
            onChange={(event) => updateField('collectionId', event.target.value)}
            required
            value={form.collectionId}
          >
            <option value="">Select collection</option>
            {collections.map((collection) => (
              <option key={collection.id} value={collection.id}>
                {collection.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label className="checkbox-field">
        <input
          checked={form.isActive}
          onChange={(event) => updateField('isActive', event.target.checked)}
          type="checkbox"
        />
        Active product
      </label>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save product'}
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
