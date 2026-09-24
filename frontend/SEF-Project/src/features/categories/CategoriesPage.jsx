import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import { CategoryForm } from './CategoryForm';
import { filterCategories } from './categoryUtils';

function formatDate(value) {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
  }).format(new Date(value));
}

export function CategoriesPage() {
  const api = useMemberOneApi();
  const [categories, setCategories] = useState([]);
  const [filters, setFilters] = useState({
    search: '',
    isActive: '',
  });
  const [formMode, setFormMode] = useState(null);
  const [editingCategory, setEditingCategory] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const visibleCategories = useMemo(
    () => filterCategories(categories, filters),
    [categories, filters],
  );

  const loadCategories = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getCategories();
      setCategories(response);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads categories when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadCategories();
  }, [loadCategories]);

  function updateFilter(field, value) {
    setFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function startCreate() {
    setEditingCategory(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(category) {
    setEditingCategory(category);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingCategory(null);
  }

  async function handleSubmitCategory(category) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingCategory) {
        await api.updateCategory(editingCategory.id, category);
        setMessage('Category updated successfully.');
      } else {
        await api.createCategory(category);
        setMessage('Category created successfully.');
      }

      closeForm();
      await loadCategories();
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(category) {
    const confirmed = window.confirm(
      `Deactivate "${category.name}"? Products can still keep their category relationship, but this category will be marked inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteCategory(category.id);
      setMessage('Category deactivated successfully.');
      await loadCategories();
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Category Management"
      description="Maintain product categories used by the catalog and product management screens."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search categories"
            onChange={(event) => updateFilter('search', event.target.value)}
            placeholder="Search by category name or description"
            value={filters.search}
          />

          <select
            aria-label="Filter category status"
            onChange={(event) => updateFilter('isActive', event.target.value)}
            value={filters.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </div>

        <button type="button" onClick={startCreate}>
          Create category
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert message={error} onRetry={loadCategories} />

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit category' : 'Create category'}</h2>
          <CategoryForm
            initialValue={editingCategory}
            isSubmitting={isSaving}
            key={editingCategory?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitCategory}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading categories..." />
      ) : visibleCategories.length === 0 ? (
        <div className="empty-state">
          No categories matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Status</th>
                <th>Created</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleCategories.map((category) => (
                <tr key={category.id}>
                  <td>{category.name}</td>
                  <td>{category.description || '-'}</td>
                  <td>
                    <span className={`status-pill ${category.isActive ? 'is-active' : 'is-inactive'}`}>
                      {category.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>{formatDate(category.createdAt)}</td>
                  <td>{formatDate(category.updatedAt)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(category)}
                        type="button"
                      >
                        Edit
                      </button>
                      <button
                        className="button-danger"
                        onClick={() => handleDelete(category)}
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
