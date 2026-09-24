import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { navigateTo } from '../../hooks/useLocation';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { VariantForm } from './VariantForm';
import {
  getVariantColourName,
  getVariantSizeName,
  getVariantStock,
  normalizeProductList,
} from './variantUtils';

function normalizeError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  if (error?.isConflict) {
    return error.detail
      || error.title
      || 'Variant conflict. Check for a duplicate SKU or duplicate Product + Size + Colour combination.';
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

function formatPrice(value) {
  if (typeof value !== 'number') {
    return '-';
  }

  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'LKR',
    maximumFractionDigits: 2,
  }).format(value);
}

function getInitialProductId() {
  return new URLSearchParams(window.location.search).get('productId') ?? '';
}

export function VariantsPage() {
  const api = useMemberOneApi();
  const [products, setProducts] = useState([]);
  const [sizes, setSizes] = useState([]);
  const [colours, setColours] = useState([]);
  const [variants, setVariants] = useState([]);
  const [selectedProductId, setSelectedProductId] = useState(getInitialProductId);
  const [formMode, setFormMode] = useState(null);
  const [editingVariant, setEditingVariant] = useState(null);
  const [isLoadingLookups, setIsLoadingLookups] = useState(true);
  const [isLoadingVariants, setIsLoadingVariants] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const selectedProduct = useMemo(
    () => products.find((product) => String(product.id) === String(selectedProductId)),
    [products, selectedProductId],
  );

  const activeSizes = useMemo(
    () => sizes.filter((size) => size.isActive !== false),
    [sizes],
  );

  const activeColours = useMemo(
    () => colours.filter((colour) => colour.isActive !== false),
    [colours],
  );

  const loadLookups = useCallback(async () => {
    setIsLoadingLookups(true);
    setError(null);

    try {
      const [productResponse, sizeResponse, colourResponse] = await Promise.all([
        api.getProducts({ page: 1, pageSize: 100, sortBy: 'name', sortDirection: 'asc' }),
        api.getSizes(),
        api.getColours(),
      ]);

      setProducts(normalizeProductList(productResponse));
      setSizes(sizeResponse);
      setColours(colourResponse);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsLoadingLookups(false);
    }
  }, [api]);

  const loadVariants = useCallback(async () => {
    if (!selectedProductId) {
      setVariants([]);
      return;
    }

    setIsLoadingVariants(true);
    setError(null);

    try {
      const response = await api.getProductVariants(selectedProductId);
      setVariants(response);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsLoadingVariants(false);
    }
  }, [api, selectedProductId]);

  useEffect(() => {
    // This effect intentionally loads products, sizes and colours when the
    // authenticated API client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLookups();
  }, [loadLookups]);

  useEffect(() => {
    // This effect intentionally reloads variants when the selected product
    // changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadVariants();
  }, [loadVariants]);

  function handleSelectProduct(productId) {
    setSelectedProductId(productId);
    setFormMode(null);
    setEditingVariant(null);
    setMessage(null);
    setError(null);

    if (productId) {
      navigateTo(`/variants?productId=${encodeURIComponent(productId)}`);
    } else {
      navigateTo('/variants');
    }
  }

  function startCreate() {
    setEditingVariant(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(variant) {
    setEditingVariant(variant);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingVariant(null);
  }

  async function handleSubmitVariant(variant) {
    if (!selectedProductId) {
      setError('Select a product before creating a variant.');
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingVariant) {
        await api.updateVariant(editingVariant.id, variant);
        setMessage('Variant updated successfully.');
      } else {
        await api.createVariant(selectedProductId, variant);
        setMessage('Variant created successfully.');
      }

      closeForm();
      await loadVariants();
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(variant) {
    const confirmed = window.confirm(
      `Deactivate SKU "${variant.sku}"? Existing stock history will remain linked to the variant.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteVariant(variant.id);
      setMessage('Variant deactivated successfully.');
      await loadVariants();
    } catch (err) {
      setError(normalizeError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Product Variant Management"
      description="Manage SKUs, size and colour combinations, price and visible stock for each product."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <select
            aria-label="Select product"
            disabled={isLoadingLookups}
            onChange={(event) => handleSelectProduct(event.target.value)}
            value={selectedProductId}
          >
            <option value="">Select a product</option>
            {products.map((product) => (
              <option key={product.id} value={product.id}>
                {product.name}
              </option>
            ))}
          </select>
        </div>

        <button
          disabled={!selectedProductId || isLoadingLookups}
          onClick={startCreate}
          type="button"
        >
          Create variant
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      {error && <Alert tone="danger">{error}</Alert>}

      {selectedProduct && (
        <section className="panel">
          <h2>{selectedProduct.name}</h2>
          <p>{selectedProduct.description || 'No product description provided.'}</p>
        </section>
      )}

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit variant' : 'Create variant'}</h2>
          <VariantForm
            colours={activeColours}
            initialValue={editingVariant}
            isSubmitting={isSaving}
            key={editingVariant?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitVariant}
            sizes={activeSizes}
          />
        </section>
      )}

      {isLoadingLookups || isLoadingVariants ? (
        <LoadingState message="Loading variants..." />
      ) : !selectedProductId ? (
        <div className="empty-state">
          Select a product to view and manage its variants.
        </div>
      ) : variants.length === 0 ? (
        <div className="empty-state">
          This product has no variants yet.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>SKU</th>
                <th>Size</th>
                <th>Colour</th>
                <th>Price</th>
                <th>Stock</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {variants.map((variant) => (
                <tr key={variant.id}>
                  <td>{variant.sku}</td>
                  <td>{getVariantSizeName(variant)}</td>
                  <td>{getVariantColourName(variant)}</td>
                  <td>{formatPrice(variant.price)}</td>
                  <td>{getVariantStock(variant)}</td>
                  <td>
                    <span className={`status-pill ${variant.isActive ? 'is-active' : 'is-inactive'}`}>
                      {variant.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(variant)}
                        type="button"
                      >
                        Edit
                      </button>
                      <button
                        className="button-danger"
                        onClick={() => handleDelete(variant)}
                        type="button"
                      >
                        Deactivate
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageShell>
  );
}
