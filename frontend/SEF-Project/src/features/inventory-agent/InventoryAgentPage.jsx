import { useCallback, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { STAFF_ROLES } from '../../utils/roles';
import {
  buildApprovalPayload,
  buildRejectionPayload,
  buildRevisionPayload,
  buildWorkflowRequest,
  canReviewWorkflow,
  formatStructuredValue,
  getAffectedVariantIds,
  getApprovalStatus,
  getCompletedSteps,
  getFinalOutcome,
  getRecommendations,
  getRecommendationAction,
  getRecommendationColour,
  getRecommendationCurrentStock,
  getRecommendationProduct,
  getRecommendationProposedQuantity,
  getRecommendationReason,
  getRecommendationReorderLevel,
  getRecommendationSize,
  getRecommendationSku,
  getReviewSuccessMessage,
  normalizeAgentApiError,
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
  const api = useCatalogApi();
  const { user } = useAuth();
  const [form, setForm] = useState(initialForm);
  const [workflowIdInput, setWorkflowIdInput] = useState('');
  const [workflow, setWorkflow] = useState(null);
  const [postApprovalRefresh, setPostApprovalRefresh] = useState(null);
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
  const canApproveWorkflow = canReviewWorkflow(user, STAFF_ROLES);
  const recommendations = getRecommendations(workflow);
  const validationResult = getValidationResult(workflow);
  const finalOutcome = getFinalOutcome(workflow);
  const toolSummaries = getToolSummaries(workflow);
  const completedSteps = getCompletedSteps(workflow);

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
      setPostApprovalRefresh(null);
    } catch (err) {
      setError(normalizeAgentApiError(err));
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
      setError(normalizeAgentApiError(err));
    } finally {
      setIsStarting(false);
    }
  }

  async function handleLoadWorkflow(event) {
    event.preventDefault();
    await loadWorkflow(workflowIdInput.trim());
  }

  async function refreshInventoryAfterApproval(reviewedWorkflow) {
    const affectedVariantIds = getAffectedVariantIds(reviewedWorkflow);

    try {
      const [inventoryResponse, historyResults] = await Promise.all([
        api.getInventory(),
        Promise.all(
          affectedVariantIds.map(async (variantId) => [
            variantId,
            await api.getStockHistory(variantId),
          ]),
        ),
      ]);

      setPostApprovalRefresh({
        refreshedAt: new Date().toISOString(),
        inventory: inventoryResponse,
        stockHistoryByVariant: Object.fromEntries(historyResults),
      });
    } catch (err) {
      setPostApprovalRefresh({
        refreshedAt: new Date().toISOString(),
        refreshError: normalizeAgentApiError(err),
      });
    }
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
        response = await api.approveInventoryWorkflow(
          workflowId,
          buildApprovalPayload(approvalNote),
        );
        await refreshInventoryAfterApproval(response);
      } else if (action === 'reject') {
        response = await api.rejectInventoryWorkflow(
          workflowId,
          buildRejectionPayload(approvalNote),
        );
        setPostApprovalRefresh(null);
      } else {
        response = await api.reviseInventoryWorkflow(
          workflowId,
          buildRevisionPayload(revisionRequest),
        );
        setPostApprovalRefresh(null);
      }

      setWorkflow(response);
      setWorkflowIdInput(getWorkflowId(response));
      setMessage(getReviewSuccessMessage(action));
      setApprovalNote('');
      setRevisionRequest('');
    } catch (err) {
      setError(normalizeAgentApiError(err));
    } finally {
      setIsReviewing(false);
    }
  }

  return (
    <PageShell
      eyebrow="Clothic · AI agent"
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
      <ApiErrorAlert
        message={error}
        onRetry={workflowId ? () => loadWorkflow(workflowId) : null}
      />

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
              <strong>{recommendations.length}</strong>
            </article>
            <article className="summary-card">
              <span>Completed steps</span>
              <strong>{completedSteps.length}</strong>
            </article>
            <article className="summary-card">
              <span>Tool summaries</span>
              <strong>{toolSummaries.length}</strong>
            </article>
          </section>

          <StructuredSection title="Plan" value={getWorkflowPlan(workflow)} />
          <StructuredSection title="Completed Steps" value={completedSteps} />
          <StructuredSection title="Tool Execution Summaries" value={toolSummaries} />
          <StructuredSection title="Validation Result" value={validationResult} />
          <StructuredSection title="Final Outcome" value={finalOutcome} />
          <StructuredSection title="Errors / Safe Failure" value={getWorkflowErrors(workflow)} />

          {status === 'PendingApproval' && (
            <section className="panel approval-review-panel">
              <div className="panel__header">
                <div>
                  <h2>Human approval review</h2>
                  <p>
                    Review deterministic validation and recommendations before calling the backend approval endpoint.
                  </p>
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
                  <dt>Workflow status</dt>
                  <dd>{status}</dd>
                </div>
                <div>
                  <dt>Approval status</dt>
                  <dd>{getApprovalStatus(workflow)}</dd>
                </div>
                <div>
                  <dt>Execution summary</dt>
                  <dd>{formatStructuredValue(finalOutcome)}</dd>
                </div>
              </dl>

              <StructuredSection
                title="Deterministic Validation Result"
                value={validationResult}
              />

              {recommendations.length === 0 ? (
                <div className="empty-state">
                  No structured recommendations were returned for review.
                </div>
              ) : (
                <div className="table-card">
                  <table className="data-table">
                    <caption className="table-caption">
                      Inventory agent recommendations awaiting human approval
                    </caption>
                    <thead>
                      <tr>
                        <th>Product</th>
                        <th>SKU</th>
                        <th>Size</th>
                        <th>Colour</th>
                        <th>Current stock</th>
                        <th>Reorder level</th>
                        <th>Recommended action</th>
                        <th>Proposed quantity</th>
                        <th>Reason</th>
                      </tr>
                    </thead>
                    <tbody>
                      {recommendations.map((recommendation, index) => (
                        <tr key={`${getRecommendationSku(recommendation)}-${index}`}>
                          <td>{getRecommendationProduct(recommendation)}</td>
                          <td>{getRecommendationSku(recommendation)}</td>
                          <td>{getRecommendationSize(recommendation)}</td>
                          <td>{getRecommendationColour(recommendation)}</td>
                          <td>{getRecommendationCurrentStock(recommendation)}</td>
                          <td>{getRecommendationReorderLevel(recommendation)}</td>
                          <td>{getRecommendationAction(recommendation)}</td>
                          <td>{getRecommendationProposedQuantity(recommendation)}</td>
                          <td>{getRecommendationReason(recommendation)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {!canApproveWorkflow && (
                <Alert tone="danger">
                  You can view this workflow, but your current role cannot approve, reject or request revisions.
                </Alert>
              )}

              {canApproveWorkflow && (
                <>
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
                </>
              )}
            </section>
          )}

          {postApprovalRefresh && (
            <StructuredSection
              title="Post-approval Inventory / Stock History Refresh"
              value={postApprovalRefresh}
            />
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
