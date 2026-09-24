import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import { CollectionForm } from './CollectionForm';
import { filterCollections } from './collectionUtils';

function formatDate(value) {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
  }).format(new Date(value));
}

function getProductCount(collection) {
  return collection.productCount ?? collection.productsCount ?? collection.products?.length ?? '-';
}

export function CollectionsPage() {
  const api = useMemberOneApi();
  const [collections, setCollections] = useState([]);
  const [filters, setFilters] = useState({
    search: '',
    isActive: '',
  });
  const [formMode, setFormMode] = useState(null);
  const [editingCollection, setEditingCollection] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const visibleCollections = useMemo(
    () => filterCollections(collections, filters),
    [collections, filters],
  );

  const loadCollections = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getCollections();
      setCollections(response);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads collections when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadCollections();
  }, [loadCollections]);

  function updateFilter(field, value) {
    setFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function startCreate() {
    setEditingCollection(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(collection) {
    setEditingCollection(collection);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingCollection(null);
  }

  async function handleSubmitCollection(collection) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingCollection) {
        await api.updateCollection(editingCollection.id, collection);
        setMessage('Collection updated successfully.');
      } else {
        await api.createCollection(collection);
        setMessage('Collection created successfully.');
      }

      closeForm();
      await loadCollections();
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(collection) {
    const confirmed = window.confirm(
      `Deactivate "${collection.name}"? Products can still keep their collection relationship, but this collection will be marked inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteCollection(collection.id);
      setMessage('Collection deactivated successfully.');
      await loadCollections();
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Collection Management"
      description="Maintain product collections used to group catalog items for menus and campaigns."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search collections"
            onChange={(event) => updateFilter('search', event.target.value)}
            placeholder="Search by collection name or description"
            value={filters.search}
          />

          <select
            aria-label="Filter collection status"
            onChange={(event) => updateFilter('isActive', event.target.value)}
            value={filters.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </div>

        <button type="button" onClick={startCreate}>
          Create collection
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert message={error} onRetry={loadCollections} />

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit collection' : 'Create collection'}</h2>
          <CollectionForm
            initialValue={editingCollection}
            isSubmitting={isSaving}
            key={editingCollection?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitCollection}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading collections..." />
      ) : visibleCollections.length === 0 ? (
        <div className="empty-state">
          No collections matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Status</th>
                <th>Products</th>
                <th>Created</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleCollections.map((collection) => (
                <tr key={collection.id}>
                  <td>{collection.name}</td>
                  <td>{collection.description || '-'}</td>
                  <td>
                    <span className={`status-pill ${collection.isActive ? 'is-active' : 'is-inactive'}`}>
                      {collection.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>{getProductCount(collection)}</td>
                  <td>{formatDate(collection.createdAt)}</td>
                  <td>{formatDate(collection.updatedAt)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(collection)}
                        type="button"
                      >
                        Edit
                      </button>
                      <button
                        className="button-danger"
                        onClick={() => handleDelete(collection)}
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
