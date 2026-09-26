import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import { ColourForm } from './ColourForm';
import {
  filterColours,
  isValidHexCode,
} from './colourUtils';

function ColourSwatch({ hexCode }) {
  if (!hexCode || !isValidHexCode(hexCode)) {
    return <span className="muted-text">No colour value</span>;
  }

  return (
    <span className="colour-swatch">
      <span
        aria-hidden="true"
        className="colour-swatch__sample"
        style={{ backgroundColor: hexCode }}
      />
      <span>{hexCode}</span>
    </span>
  );
}

export function ColoursPage() {
  const api = useCatalogApi();
  const [colours, setColours] = useState([]);
  const [filters, setFilters] = useState({
    search: '',
    isActive: '',
  });
  const [formMode, setFormMode] = useState(null);
  const [editingColour, setEditingColour] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const visibleColours = useMemo(
    () => filterColours(colours, filters),
    [colours, filters],
  );

  const loadColours = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getColours();
      setColours(response);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads colours when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadColours();
  }, [loadColours]);

  function updateFilter(field, value) {
    setFilters((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function startCreate() {
    setEditingColour(null);
    setFormMode('create');
    setMessage(null);
    setError(null);
  }

  function startEdit(colour) {
    setEditingColour(colour);
    setFormMode('edit');
    setMessage(null);
    setError(null);
  }

  function closeForm() {
    setFormMode(null);
    setEditingColour(null);
  }

  async function handleSubmitColour(colour) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (formMode === 'edit' && editingColour) {
        await api.updateColour(editingColour.id, colour);
        setMessage('Colour updated successfully.');
      } else {
        await api.createColour(colour);
        setMessage('Colour created successfully.');
      }

      closeForm();
      await loadColours();
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(colour) {
    const confirmed = window.confirm(
      `Deactivate "${colour.name}"? Existing variants can still keep this colour relationship, but the colour will be marked inactive.`,
    );

    if (!confirmed) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api.deleteColour(colour.id);
      setMessage('Colour deactivated successfully.');
      await loadColours();
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }

  return (
    <PageShell
      eyebrow="Clothic · Catalog"
      title="Colour Management"
      description="Maintain reusable product colours and hex values used when creating product variants."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Search colours"
            onChange={(event) => updateFilter('search', event.target.value)}
            placeholder="Search by colour name or hex code"
            value={filters.search}
          />

          <select
            aria-label="Filter colour status"
            onChange={(event) => updateFilter('isActive', event.target.value)}
            value={filters.isActive}
          >
            <option value="">All statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </div>

        <button type="button" onClick={startCreate}>
          Create colour
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert message={error} onRetry={loadColours} />

      {formMode && (
        <section className="panel">
          <h2>{formMode === 'edit' ? 'Edit colour' : 'Create colour'}</h2>
          <ColourForm
            initialValue={editingColour}
            isSubmitting={isSaving}
            key={editingColour?.id ?? formMode}
            onCancel={closeForm}
            onSubmit={handleSubmitColour}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading colours..." />
      ) : visibleColours.length === 0 ? (
        <div className="empty-state">
          No colours matched the current filters.
        </div>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <caption className="table-caption">
              Product colours with hex code, status and management actions
            </caption>
            <thead>
              <tr>
                <th>Name</th>
                <th>Hex code</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleColours.map((colour) => (
                <tr key={colour.id}>
                  <td>{colour.name}</td>
                  <td>
                    <ColourSwatch hexCode={colour.hexCode} />
                  </td>
                  <td>
                    <span className={`status-pill ${colour.isActive ? 'is-active' : 'is-inactive'}`}>
                      {colour.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="button-secondary"
                        onClick={() => startEdit(colour)}
                        type="button"
                      >
                        Edit
                      </button>
                      {colour.isActive && <button
                        className="button-danger"
                        onClick={() => handleDelete(colour)}
                        type="button"
                      >Deactivate</button>}
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
