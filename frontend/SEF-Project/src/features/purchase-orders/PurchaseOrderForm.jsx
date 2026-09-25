import { useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import {
  buildPurchaseOrderPayload,
  calculatePurchaseOrderTotal,
  emptyPurchaseOrderItem,
  validatePurchaseOrderForm,
} from './purchaseOrderUtils';

function variantLabel(option) {
  if (!option) {
    return '';
  }

  const name = option.productName
    ? `${option.productName} · ${option.variantName}`
    : option.variantName;

  return option.sku ? `${name} (${option.sku})` : name;
}

export function PurchaseOrderForm({
  suppliers,
  variantOptions,
  isSubmitting,
  onCancel,
  onSubmit,
}) {
  const [form, setForm] = useState(() => ({
    supplierId: '',
    expectedAt: '',
    notes: '',
    items: [emptyPurchaseOrderItem()],
  }));
  const [validationErrors, setValidationErrors] = useState([]);

  function updateField(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function updateItem(index, field, value) {
    setForm((current) => ({
      ...current,
      items: current.items.map((item, itemIndex) =>
        itemIndex === index ? { ...item, [field]: value } : item),
    }));
  }

  function addItem() {
    setForm((current) => ({
      ...current,
      items: [...current.items, emptyPurchaseOrderItem()],
    }));
  }

  function removeItem(index) {
    setForm((current) => ({
      ...current,
      items: current.items.filter((_, itemIndex) => itemIndex !== index),
    }));
  }

  function handleSubmit(event) {
    event.preventDefault();

    const errors = validatePurchaseOrderForm(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    onSubmit(buildPurchaseOrderPayload(form));
  }

  const total = calculatePurchaseOrderTotal(form.items);

  return (
    <form className="entity-form" noValidate onSubmit={handleSubmit}>
      {validationErrors.length > 0 && (
        <Alert tone="danger">
          <ul className="error-list">
            {validationErrors.map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </Alert>
      )}

      <div className="form-grid">
        <label>
          Supplier
          <select
            onChange={(event) => updateField('supplierId', event.target.value)}
            required
            value={form.supplierId}
          >
            <option value="">Select a supplier</option>
            {(suppliers ?? []).map((supplier) => (
              <option key={supplier.id} value={supplier.id}>
                {supplier.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Expected date
          <input
            onChange={(event) => updateField('expectedAt', event.target.value)}
            type="date"
            value={form.expectedAt}
          />
        </label>
      </div>

      <label>
        Notes
        <textarea
          maxLength={2000}
          onChange={(event) => updateField('notes', event.target.value)}
          rows={3}
          value={form.notes}
        />
      </label>

      <fieldset className="purchase-order-lines">
        <legend>Order lines</legend>

        {(variantOptions ?? []).length === 0 && (
          <Alert>
            No product variants are available to order. Add products and variants
            in the catalog first.
          </Alert>
        )}

        {form.items.map((item, index) => (
          <div className="purchase-order-line" key={index}>
            <label>
              Product variant
              <select
                onChange={(event) =>
                  updateItem(index, 'productVariantId', event.target.value)}
                required
                value={item.productVariantId}
              >
                <option value="">Select a variant</option>
                {(variantOptions ?? []).map((option) => (
                  <option key={option.id} value={option.id}>
                    {variantLabel(option)}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Quantity
              <input
                min="1"
                onChange={(event) =>
                  updateItem(index, 'quantity', event.target.value)}
                required
                step="1"
                type="number"
                value={item.quantity}
              />
            </label>

            <label>
              Unit cost
              <input
                min="0"
                onChange={(event) =>
                  updateItem(index, 'unitCost', event.target.value)}
                required
                step="0.01"
                type="number"
                value={item.unitCost}
              />
            </label>

            <button
              className="button-secondary"
              disabled={form.items.length === 1}
              onClick={() => removeItem(index)}
              type="button"
            >
              Remove
            </button>
          </div>
        ))}

        <div className="form-actions">
          <button className="button-secondary" onClick={addItem} type="button">
            Add line
          </button>
        </div>
      </fieldset>

      <p className="muted-text">
        Estimated order total: {total.toFixed(2)}
      </p>

      <div className="form-actions">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Create draft'}
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

export default PurchaseOrderForm;
