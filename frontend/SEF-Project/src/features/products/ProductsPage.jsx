import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { ProductForm } from './ProductForm';

const defaultQuery = {
  search: '',
  categoryId: '',
  collectionId: '',
  isActive: '',
  sortBy: 'name',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};

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

function normalizeError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

export function ProductsPage() {
  const api = useMemberOneApi();
  const { isStaffOrAdmin } = useAuth();
  const [query, setQuery] = useState(defaultQuery);
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
    () => ({
      search: query.search,
      categoryId: query.categoryId,
      collectionId: query.collectionId,
      isActive: query.isActive,
      sortBy: query.sortBy,
      sortDirection: query.sortDirection,
      page: query.page,
      pageSize: query.pageSize,
    }),
    [query],
  );

  const loadProducts = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getProducts(productQuery);
      setProductsResponse(response);
    } catch (err) {
      setError(normalizeError(err));
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
      setError(normalizeError(err));
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
    setQuery((current) => ({
      ...current,
      [field]: value,
      page: field === 'page' ? value : 1,
    }));
  }

  async function handleView(productId) {
    setError(null);
    setMessage(null);

    try {
      const product = await api.getProduct(productId);
      setSelectedProduct(product);
    } catch (err) {
      setError(normalizeError(err));
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
      setError(normalizeError(err));
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
      setError(normalizeError(err));
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
        </div>

        {isStaffOrAdmin && (
          <button type="button" onClick={startCreate}>
            Create product
          </button>
        )}
      </div>

      {message && <Alert>{message}</Alert>}
      {error && <Alert tone="danger">{error}</Alert>}

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

      <div className="pagination-bar">
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
          {' '}({productsResponse?.totalItems ?? 0} products)
        </span>
        <button
          className="button-secondary"
          disabled={query.page >= totalPages}
          onClick={() => updateQuery('page', query.page + 1)}
          type="button"
        >
          Next
        </button>
      </div>

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
