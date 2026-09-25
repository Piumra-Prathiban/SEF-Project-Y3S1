import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { ProductForm } from './ProductForm';
import {
  buildProductQuery,
  defaultProductQuery,
  updatePagedQuery,
} from './productQueryUtils';
import { normalizeApiError } from '../../utils/apiErrorUtils';

function formatDate(value) {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
  }).format(new Date(value));
}

function getBasePrice(product) {
  const prices = product.variants
    ?.map((variant) => variant.price)
    .filter((price) => typeof price === 'number') ?? [];

  if (prices.length === 0) {
    return '-';
  }

  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'LKR',
    maximumFractionDigits: 2,
  }).format(Math.min(...prices));
}

export function ProductsPage() {
  const api = useMemberOneApi();
  const navigate = useNavigate();
  const { isStaffOrAdmin } = useAuth();
  const [query, setQuery] = useState(defaultProductQuery);
  const [productsResponse, setProductsResponse] = useState(null);
  const [categories, setCategories] = useState([]);
  const [collections, setCollections] = useState([]);
  const [selectedProduct, setSelectedProduct] = useState(null);
  const [formMode, setFormMode] = useState(null);
  const [editingProduct, setEditingProduct] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const productItems = productsResponse?.items ?? [];
  const totalPages = productsResponse?.totalPages ?? 1;

  const productQuery = useMemo(
    () => buildProductQuery(query),
    [query],
  );

  const loadProducts = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getProducts(productQuery);
      setProductsResponse(response);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api, productQuery]);

  const loadLookups = useCallback(async () => {
    try {
      const [categoryResponse, collectionResponse] = await Promise.all([
        api.getCategories(),
        api.getCollections(),
      ]);

      setCategories(categoryResponse);
      setCollections(collectionResponse);
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads lookup data when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLookups();
  }, [loadLookups]);

  useEffect(() => {
    // This effect intentionally reloads products when list query parameters
    // change.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadProducts();
  }, [loadProducts]);

  function updateQuery(field, value) {
    setQuery((current) => updatePagedQuery(current, field, value));
  }

  async function handleView(productId) {
    setError(null);
    setMessage(null);

    try {
      const product = await api.getProduct(productId);
      setSelectedProduct(product);
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  function startCreate() {
    setEditingProduct(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(product) {
    setEditingProduct(product);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingProduct(null);
  }

  async function handleSubmitProduct(product) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingProduct) {
        await api.updateProduct(editingProduct.id, product);
        setMessage('Product updated successfully.');
      } else {
        await api.createProduct(product);
        setMessage('Product created successfully.');
      }

      closeForm();
      await loadProducts();
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(product) {
    const confirmed = window.confirm(
      `Deactivate "${product.name}"? This will mark the product as inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteProduct(product.id);
      setMessage('Product deactivated successfully.');

      if (selectedProduct?.id === product.id) {
        setSelectedProduct(null);
      }

      await loadProducts();
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Product Management"
      description="Search, filter and maintain product catalog records connected to categories, collections and variants."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search products"
            onChange={(event) => updateQuery('search', event.target.value)}
            placeholder="Search by product name"
            value={query.search}
          />

          <select
            aria-label="Filter by category"
            onChange={(event) => updateQuery('categoryId', event.target.value)}
            value={query.categoryId}
          >
            <option value="">All categories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <select
            aria-label="Filter by collection"
            onChange={(event) => updateQuery('collectionId', event.target.value)}
            value={query.collectionId}
          >
            <option value="">All collections</option>
            {collections.map((collection) => (
              <option key={collection.id} value={collection.id}>
                {collection.name}
              </option>
            ))}
          </select>

          <select
            aria-label="Filter by active status"
            onChange={(event) => updateQuery('isActive', event.target.value)}
            value={query.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>

          <input
            aria-label="Minimum product price"
            min="0"
            onChange={(event) => updateQuery('minPrice', event.target.value)}
            placeholder="Min price"
            type="number"
            value={query.minPrice}
          />

          <input
            aria-label="Maximum product price"
            min="0"
            onChange={(event) => updateQuery('maxPrice', event.target.value)}
            placeholder="Max price"
            type="number"
            value={query.maxPrice}
          />

          <select
            aria-label="Sort products"
            onChange={(event) => updateQuery('sortBy', event.target.value)}
            value={query.sortBy}
          >
            <option value="name">Name</option>
            <option value="price">Base price</option>
            <option value="createdAt">Created date</option>
          </select>

          <select
            aria-label="Sort direction"
            onChange={(event) => updateQuery('sortDirection', event.target.value)}
            value={query.sortDirection}
          >
            <option value="asc">Ascending</option>
            <option value="desc">Descending</option>
          </select>

          <select
            aria-label="Product page size"
            onChange={(event) => updateQuery('pageSize', Number(event.target.value))}
            value={query.pageSize}
          >
            <option value={10}>10 per page</option>
            <option value={25}>25 per page</option>
            <option value={50}>50 per page</option>
          </select>
        </div>

        {isStaffOrAdmin && (
          <button type="button" onClick={startCreate}>
            Create product
          </button>
        )}
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert message={error} onRetry={loadProducts} />

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit product' : 'Create product'}</h2>
          <ProductForm
            categories={categories}
            collections={collections}
            initialValue={editingProduct}
            isSubmitting={isSaving}
            key={editingProduct?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitProduct}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading products..." />
      ) : productItems.length === 0 ? (
        <div className="empty-state">
          No products matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <caption className="table-caption">
              Product search results with category, collection, price, status and actions
            </caption>
            <thead>
              <tr>
                <th>Name</th>
                <th>Category</th>
                <th>Collection</th>
                <th>Base price</th>
                <th>Status</th>
                <th>Variants</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {productItems.map((product) => (
                <tr key={product.id}>
                  <td>{product.name}</td>
                  <td>{product.categoryName || '-'}</td>
                  <td>{product.collectionName || '-'}</td>
                  <td>{getBasePrice(product)}</td>
                  <td>
                    <span className={`status-pill ${product.isActive ? 'is-active' : 'is-inactive'}`}>
                      {product.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>{product.variants?.length ?? 0}</td>
                  <td>{formatDate(product.createdAt)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => handleView(product.id)}
                        type="button"
                      >
                        View
                      </button>
                      <button
                        className="button-secondary"
                        onClick={() => navigate(`/variants?productId=${encodeURIComponent(product.id)}`)}
                        type="button"
                      >
                        Variants
                      </button>
                      {isStaffOrAdmin && (
                        <>
                          <button
                            className="button-secondary"
                            onClick={() => startEdit(product)}
                            type="button"
                          >
                            Edit
                          </button>
                          <button
                            className="button-danger"
                            onClick={() => handleDelete(product)}
                            type="button"
                          >
                            Deactivate
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <nav className="pagination-bar" aria-label="Product pagination">
        <button
          className="button-secondary"
          disabled={query.page <= 1}
          onClick={() => updateQuery('page', query.page - 1)}
          type="button"
        >
          Previous
        </button>
        <span>
          Page {productsResponse?.page ?? query.page} of {totalPages || 1}
          {' '}({productsResponse?.totalItems ?? 0} products, page size {productsResponse?.pageSize ?? query.pageSize})
        </span>
        <button
          className="button-secondary"
          disabled={query.page >= totalPages}
          onClick={() => updateQuery('page', query.page + 1)}
          type="button"
        >
          Next
        </button>
      </nav>

      {selectedProduct && (
        <section className="panel detail-panel">
          <div className="panel__header">
            <div>
              <h2>{selectedProduct.name}</h2>
              <p>{selectedProduct.description || 'No description provided.'}</p>
            </div>
            <button
              className="button-secondary"
              onClick={() => setSelectedProduct(null)}
              type="button"
            >
              Close
            </button>
          </div>

          <dl className="detail-grid">
            <div>
              <dt>Category</dt>
              <dd>{selectedProduct.categoryName || '-'}</dd>
            </div>
            <div>
              <dt>Collection</dt>
              <dd>{selectedProduct.collectionName || '-'}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd>{selectedProduct.isActive ? 'Active' : 'Inactive'}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatDate(selectedProduct.createdAt)}</dd>
            </div>
            <div>
              <dt>Updated</dt>
              <dd>{formatDate(selectedProduct.updatedAt)}</dd>
            </div>
            <div>
              <dt>Variants</dt>
              <dd>{selectedProduct.variants?.length ?? 0}</dd>
            </div>
          </dl>
        </section>
      )}
    </PageShell>
  );
}
