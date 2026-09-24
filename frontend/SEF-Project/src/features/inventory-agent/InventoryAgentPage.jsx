import { useCallback, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import {
  buildWorkflowRequest,
  formatStructuredValue,
  getApprovalStatus,
  getCompletedSteps,
  getFinalOutcome,
  getRecommendations,
  getToolSummaries,
  getValidationResult,
  getWorkflowErrors,
  getWorkflowId,
  getWorkflowObjective,
  getWorkflowPlan,
  getWorkflowStatus,
  INVENTORY_AGENT_STATUSES,
  validateWorkflowRequest,
} from './inventoryAgentUtils';

const initialForm = {
  objective: 'Analyze the current inventory and identify variants that should be restocked.',
  variantIds: '',
};

function normalizeError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

function getStatusClass(status) {
  if (['Failed', 'Rejected'].includes(status)) {
    return 'is-danger';
  }

  if (['PendingApproval', 'RevisionRequested', 'Validation'].includes(status)) {
    return 'is-warning';
  }

  if (['Approved', 'Completed'].includes(status)) {
    return 'is-active';
  }

  return 'is-inactive';
}

function StructuredSection({ title, value }) {
  const formatted = formatStructuredValue(value);

  return (
    <section className="structured-section">
      <h2>{title}</h2>
      {formatted === '-' ? (
        <p className="muted-text">Not available yet.</p>
      ) : (
        <pre className="structured-output">{formatted}</pre>
      )}
    </section>
  );
}

export function InventoryAgentPage() {
  const api = useMemberOneApi();
  const [form, setForm] = useState(initialForm);
  const [workflowIdInput, setWorkflowIdInput] = useState('');
  const [workflow, setWorkflow] = useState(null);
  const [validationErrors, setValidationErrors] = useState([]);
  const [approvalNote, setApprovalNote] = useState('');
  const [revisionRequest, setRevisionRequest] = useState('');
  const [isStarting, setIsStarting] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isReviewing, setIsReviewing] = useState(false);
  const [message, setMessage] = useState(null);
  const [error, setError] = useState(null);

  const workflowId = getWorkflowId(workflow);
  const status = getWorkflowStatus(workflow);

  const loadWorkflow = useCallback(async (id) => {
    if (!id) {
      return;
    }

    setIsRefreshing(true);
    setError(null);

    try {
      const response = await api.getInventoryWorkflow(id);
      setWorkflow(response);
      setWorkflowIdInput(getWorkflowId(response));
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsRefreshing(false);
    }
  }, [api]);

  function updateForm(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  async function handleStartWorkflow(event) {
    event.preventDefault();

    const errors = validateWorkflowRequest(form);
    setValidationErrors(errors);

    if (errors.length > 0) {
      return;
    }

    setIsStarting(true);
    setMessage(null);
    setError(null);

    try {
      const response = await api.createInventoryWorkflow(buildWorkflowRequest(form));
      setWorkflow(response);
      setWorkflowIdInput(getWorkflowId(response));
      setMessage('Inventory analysis workflow started.');
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsStarting(false);
    }
  }

  async function handleLoadWorkflow(event) {
    event.preventDefault();
    await loadWorkflow(workflowIdInput.trim());
  }

  async function handleReview(action) {
    if (!workflowId) {
      setError('Load a workflow before submitting a review action.');
      return;
    }

    setIsReviewing(true);
    setError(null);
    setMessage(null);

    try {
      let response;

      if (action === 'approve') {
        response = await api.approveInventoryWorkflow(workflowId, {
          note: approvalNote.trim() || null,
        });
        setMessage('Workflow approved.');
      } else if (action === 'reject') {
        response = await api.rejectInventoryWorkflow(workflowId, {
          reason: approvalNote.trim() || 'Rejected from frontend review.',
        });
        setMessage('Workflow rejected.');
      } else {
        response = await api.reviseInventoryWorkflow(workflowId, {
          revisionRequest: revisionRequest.trim(),
        });
        setMessage('Workflow revision requested.');
      }

      setWorkflow(response);
      setWorkflowIdInput(getWorkflowId(response));
      setApprovalNote('');
      setRevisionRequest('');
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsReviewing(false);
    }
  }

  return (
    <PageShell
      eyebrow="Member 1"
      title="Inventory AI Analysis"
      description="Start and review Inventory Analysis Agent workflows using the backend workflow state as the source of truth."
    >
      <Alert>
        This page displays structured workflow state only. Hidden model reasoning and chain-of-thought are not shown.
      </Alert>

      <section className="panel">
        <h2>Start inventory analysis workflow</h2>
        <form className="entity-form" onSubmit={handleStartWorkflow}>
          {validationErrors.length > 0 && (
            <Alert tone="danger">
              <ul className="error-list">
                {validationErrors.map((validationError) => (
                  <li key={validationError}>{validationError}</li>
                ))}
              </ul>
            </Alert>
          )}

          <label>
            Objective
            <textarea
              maxLength={1000}
              onChange={(event) => updateForm('objective', event.target.value)}
              required
              rows={4}
              value={form.objective}
            />
          </label>

          <label>
            Relevant variant IDs
            <input
              onChange={(event) => updateForm('variantIds', event.target.value)}
              placeholder="Optional comma-separated variant IDs"
              value={form.variantIds}
            />
          </label>

          <div className="form-actions">
            <button disabled={isStarting} type="submit">
              {isStarting ? 'Starting...' : 'Start workflow'}
            </button>
          </div>
        </form>
      </section>

      <section className="panel">
        <h2>Load existing workflow</h2>
        <form className="entity-form" onSubmit={handleLoadWorkflow}>
          <div className="form-grid">
            <label>
              Workflow ID
              <input
                onChange={(event) => setWorkflowIdInput(event.target.value)}
                placeholder="Workflow ID"
                value={workflowIdInput}
              />
            </label>
          </div>

          <div className="form-actions">
            <button
              className="button-secondary"
              disabled={isRefreshing || !workflowIdInput.trim()}
              type="submit"
            >
              {isRefreshing ? 'Loading...' : 'Load workflow'}
            </button>
            <button
              className="button-secondary"
              disabled={isRefreshing || !workflowId}
              onClick={() => loadWorkflow(workflowId)}
              type="button"
            >
              Refresh current workflow
            </button>
          </div>
        </form>
      </section>

      {message && <Alert>{message}</Alert>}
      {error && <Alert tone="danger">{error}</Alert>}

      {isRefreshing && !workflow ? (
        <LoadingState message="Loading workflow..." />
      ) : workflow ? (
        <>
          <section className="panel">
            <div className="panel__header">
              <div>
                <h2>Workflow Overview</h2>
                <p>Workflow ID: {workflowId || '-'}</p>
              </div>
              <span className={`status-pill ${getStatusClass(status)}`}>
                {status}
              </span>
            </div>

            <dl className="detail-grid">
              <div>
                <dt>Objective</dt>
                <dd>{getWorkflowObjective(workflow)}</dd>
              </div>
              <div>
                <dt>Approval status</dt>
                <dd>{getApprovalStatus(workflow)}</dd>
              </div>
              <div>
                <dt>Known workflow states</dt>
                <dd>{INVENTORY_AGENT_STATUSES.join(', ')}</dd>
              </div>
            </dl>
          </section>

          <section className="summary-grid" aria-label="Workflow structured state">
            <article className="summary-card">
              <span>Current status</span>
              <strong>{status}</strong>
            </article>
            <article className="summary-card">
              <span>Recommendations</span>
              <strong>{getRecommendations(workflow).length}</strong>
            </article>
            <article className="summary-card">
              <span>Completed steps</span>
              <strong>{getCompletedSteps(workflow).length}</strong>
            </article>
            <article className="summary-card">
              <span>Tool summaries</span>
              <strong>{getToolSummaries(workflow).length}</strong>
            </article>
          </section>

          <StructuredSection title="Plan" value={getWorkflowPlan(workflow)} />
          <StructuredSection title="Completed Steps" value={getCompletedSteps(workflow)} />
          <StructuredSection title="Tool Execution Summaries" value={getToolSummaries(workflow)} />
          <StructuredSection title="Validation Result" value={getValidationResult(workflow)} />
          <StructuredSection title="Recommendations" value={getRecommendations(workflow)} />
          <StructuredSection title="Final Outcome" value={getFinalOutcome(workflow)} />
          <StructuredSection title="Errors / Safe Failure" value={getWorkflowErrors(workflow)} />

          {status === 'PendingApproval' && (
            <section className="panel">
              <h2>Human approval</h2>
              <p>
                Approve, reject or request revision only after reviewing the structured recommendation and validation result.
              </p>

              <label className="entity-form">
                Approval/rejection note
                <textarea
                  onChange={(event) => setApprovalNote(event.target.value)}
                  rows={3}
                  value={approvalNote}
                />
              </label>

              <label className="entity-form">
                Revision request
                <textarea
                  onChange={(event) => setRevisionRequest(event.target.value)}
                  placeholder="Required when requesting a revision"
                  rows={3}
                  value={revisionRequest}
                />
              </label>

              <div className="form-actions">
                <button
                  disabled={isReviewing}
                  onClick={() => handleReview('approve')}
                  type="button"
                >
                  Approve
                </button>
                <button
                  className="button-danger"
                  disabled={isReviewing}
                  onClick={() => handleReview('reject')}
                  type="button"
                >
                  Reject
                </button>
                <button
                  className="button-secondary"
                  disabled={isReviewing || !revisionRequest.trim()}
                  onClick={() => handleReview('revise')}
                  type="button"
                >
                  Request revision
                </button>
              </div>
            </section>
          )}
        </>
      ) : (
        <div className="empty-state">
          Start a new inventory analysis workflow or load an existing workflow ID to view persisted state.
        </div>
      )}
    </PageShell>
  );
}
