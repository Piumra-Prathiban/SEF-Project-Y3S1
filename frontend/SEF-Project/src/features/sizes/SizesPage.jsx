import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { SizeForm } from './SizeForm';
import { filterSizes } from './sizeUtils';

function normalizeError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

export function SizesPage() {
  const api = useMemberOneApi();
  const [sizes, setSizes] = useState([]);
  const [filters, setFilters] = useState({
    search: '',
    isActive: '',
  });
  const [formMode, setFormMode] = useState(null);
  const [editingSize, setEditingSize] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const visibleSizes = useMemo(
    () => filterSizes(sizes, filters),
    [sizes, filters],
  );

  const loadSizes = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getSizes();
      setSizes(response);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads sizes when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSizes();
  }, [loadSizes]);

  function updateFilter(field, value) {
    setFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function startCreate() {
    setEditingSize(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(size) {
    setEditingSize(size);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingSize(null);
  }

  async function handleSubmitSize(size) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingSize) {
        await api.updateSize(editingSize.id, size);
        setMessage('Size updated successfully.');
      } else {
        await api.createSize(size);
        setMessage('Size created successfully.');
      }

      closeForm();
      await loadSizes();
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(size) {
    const confirmed = window.confirm(
      `Deactivate "${size.name}"? Existing variants can still keep this size relationship, but the size will be marked inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteSize(size.id);
      setMessage('Size deactivated successfully.');
      await loadSizes();
    } catch (err) {
      setError(normalizeError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Size Management"
      description="Maintain reusable product size options used when creating product variants."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search sizes"
            onChange={(event) => updateFilter('search', event.target.value)}
            placeholder="Search by size name or code"
            value={filters.search}
          />

          <select
            aria-label="Filter size status"
            onChange={(event) => updateFilter('isActive', event.target.value)}
            value={filters.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </div>

        <button type="button" onClick={startCreate}>
          Create size
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      {error && <Alert tone="danger">{error}</Alert>}

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit size' : 'Create size'}</h2>
          <SizeForm
            initialValue={editingSize}
            isSubmitting={isSaving}
            key={editingSize?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitSize}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading sizes..." />
      ) : visibleSizes.length === 0 ? (
        <div className="empty-state">
          No sizes matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Code</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleSizes.map((size) => (
                <tr key={size.id}>
                  <td>{size.name}</td>
                  <td>{size.code}</td>
                  <td>
                    <span className={`status-pill ${size.isActive ? 'is-active' : 'is-inactive'}`}>
                      {size.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(size)}
                        type="button"
                      >
                        Edit
                      </button>
                      <button
                        className="button-danger"
                        onClick={() => handleDelete(size)}
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
