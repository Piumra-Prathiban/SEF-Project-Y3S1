import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import { SupplierForm } from './SupplierForm';
import { filterSuppliers } from './supplierUtils';

function formatDate(value) {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
  }).format(new Date(value));
}

export function SuppliersPage() {
  const api = useCatalogApi();
  const [suppliers, setSuppliers] = useState([]);
  const [filters, setFilters] = useState({
    search: '',
    isActive: '',
  });
  const [formMode, setFormMode] = useState(null);
  const [editingSupplier, setEditingSupplier] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const visibleSuppliers = useMemo(
    () => filterSuppliers(suppliers, filters),
    [suppliers, filters],
  );

  const loadSuppliers = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getSuppliers();
      setSuppliers(response);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads suppliers when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSuppliers();
  }, [loadSuppliers]);

  function updateFilter(field, value) {
    setFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function startCreate() {
    setEditingSupplier(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(supplier) {
    setEditingSupplier(supplier);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingSupplier(null);
  }

  async function handleSubmitSupplier(supplier) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingSupplier) {
        await api.updateSupplier(editingSupplier.id, supplier);
        setMessage('Supplier updated successfully.');
      } else {
        await api.createSupplier(supplier);
        setMessage('Supplier created successfully.');
      }

      closeForm();
      await loadSuppliers();
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(supplier) {
    const confirmed = window.confirm(
      `Deactivate "${supplier.name}"? Products can still keep their supplier relationship, but this supplier will be marked inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteSupplier(supplier.id);
      setMessage('Supplier deactivated successfully.');
      await loadSuppliers();
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Clothic · Catalog"
      title="Supplier Management"
      description="Maintain the suppliers that fulfil your catalog products and restocking orders."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search suppliers"
            onChange={(event) => updateFilter('search', event.target.value)}
            placeholder="Search by supplier, contact or email"
            value={filters.search}
          />

          <select
            aria-label="Filter supplier status"
            onChange={(event) => updateFilter('isActive', event.target.value)}
            value={filters.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </div>

        <button type="button" onClick={startCreate}>
          Create supplier
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert message={error} onRetry={loadSuppliers} />

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit supplier' : 'Create supplier'}</h2>
          <SupplierForm
            initialValue={editingSupplier}
            isSubmitting={isSaving}
            key={editingSupplier?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitSupplier}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading suppliers..." />
      ) : visibleSuppliers.length === 0 ? (
        <div className="empty-state">
          No suppliers matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <caption className="table-caption">
              Suppliers with contact details, status and management actions
            </caption>
            <thead>
              <tr>
                <th>Name</th>
                <th>Contact person</th>
                <th>Email</th>
                <th>Phone</th>
                <th>Status</th>
                <th>Created</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleSuppliers.map((supplier) => (
                <tr key={supplier.id}>
                  <td>{supplier.name}</td>
                  <td>{supplier.contactName || '-'}</td>
                  <td>{supplier.email || '-'}</td>
                  <td>{supplier.phone || '-'}</td>
                  <td>
                    <span className={`status-pill ${supplier.isActive ? 'is-active' : 'is-inactive'}`}>
                      {supplier.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>{formatDate(supplier.createdAt)}</td>
                  <td>{formatDate(supplier.updatedAt)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(supplier)}
                        type="button"
                      >
                        Edit
                      </button>
                      <button
                        className="button-danger"
                        onClick={() => handleDelete(supplier)}
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
