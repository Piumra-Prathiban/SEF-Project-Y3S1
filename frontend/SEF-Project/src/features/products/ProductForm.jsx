import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildProductPayload,
  initialProductFormState,
  validateProductForm,
} from './productFormUtils';

export function ProductForm({
  categories,
  collections,
  initialValue,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    name: initialValue?.name ?? initialProductFormState.name,
    description: initialValue?.description ?? initialProductFormState.description,
    imageUrl: initialValue?.imageUrl ?? initialProductFormState.imageUrl,
    categoryId: initialValue?.categoryId ?? initialProductFormState.categoryId,
    collectionId: initialValue?.collectionId ?? initialProductFormState.collectionId,
    supplierId: initialValue?.supplierId ?? initialProductFormState.supplierId,
    isActive: initialValue?.isActive ?? initialProductFormState.isActive,
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

    const errors = validateProductForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildProductPayload(form));
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

      <label>
        Image URL
        <input
          maxLength={500}
          onChange={(event) => updateField('imageUrl', event.target.value)}
          placeholder="https://..."
          type="url"
          value={form.imageUrl}
        />
        <small>
          Shown on the storefront; leave empty to use the placeholder tile.
        </small>
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
