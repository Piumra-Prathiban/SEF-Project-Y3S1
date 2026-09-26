import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { STAFF_ROLES } from '../../utils/roles';
import { getProductName, getSku, getVariantId, normalizeInventoryItems, normalizeStockHistoryItems } from '../inventory/inventoryDashboardUtils';
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
  getRecommendationCurrentStock,
  getRecommendationProduct,
  getRecommendationProposedQuantity,
  getRecommendationReason,
  getRecommendationReorderLevel,
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
  variantIds: [],
};

function getStatusClass(status) {
  if (['Failed', 'Cancelled'].includes(status)) {
    return 'is-danger';
  }

  if (['AwaitingApproval', 'Planning'].includes(status)) {
    return 'is-warning';
  }

  if (status === 'Completed') {
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
  const [searchParams, setSearchParams] = useSearchParams();
  const [form, setForm] = useState(initialForm);
  const [availableVariants, setAvailableVariants] = useState([]);
  const [variantSearch, setVariantSearch] = useState('');
  const [inventoryLoadError, setInventoryLoadError] = useState(null);
  const [workflowList, setWorkflowList] = useState({ items: [], page: 1, totalPages: 1, totalItems: 0 });
  const [workflowListStatus, setWorkflowListStatus] = useState('AwaitingApproval');
  const [workflowListPage, setWorkflowListPage] = useState(1);
  const [workflowListLoading, setWorkflowListLoading] = useState(true);
  const [workflowListError, setWorkflowListError] = useState(null);
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
  const linkedWorkflowId = searchParams.get('workflowId');
  const matchingVariants = availableVariants.filter((item) => `${getProductName(item)} ${getSku(item)}`.toLowerCase().includes(variantSearch.toLowerCase()));

  const loadInventoryOptions = useCallback(async () => {
    setInventoryLoadError(null);
    try {
      setAvailableVariants(normalizeInventoryItems(await api.getInventory()));
    } catch (err) {
      setInventoryLoadError(normalizeAgentApiError(err));
    }
  }, [api]);

  const loadWorkflowList = useCallback(async () => {
    setWorkflowListLoading(true);
    setWorkflowListError(null);
    try {
      const response = await api.listInventoryWorkflows({ status: workflowListStatus || undefined, page: workflowListPage, pageSize: 10 });
      if (workflowListPage > (response.totalPages ?? 1)) {
        setWorkflowListPage(response.totalPages ?? 1);
      }
      setWorkflowList({ items: response.items ?? [], page: response.page ?? 1, totalPages: response.totalPages ?? 1, totalItems: response.totalItems ?? 0 });
    } catch (err) {
      setWorkflowListError(normalizeAgentApiError(err));
    } finally {
      setWorkflowListLoading(false);
    }
  }, [api, workflowListStatus, workflowListPage]);

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

  useEffect(() => {
    // Load inventory choices once for the authenticated API client.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadInventoryOptions();
  }, [loadInventoryOptions]);

  useEffect(() => {
    // Keep the approval queue current when its filter or page changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadWorkflowList();
  }, [loadWorkflowList]);

  useEffect(() => {
    if (linkedWorkflowId && linkedWorkflowId !== workflowId) {
      // A copied workflow URL restores its review state after a refresh.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      loadWorkflow(linkedWorkflowId);
    }
  }, [linkedWorkflowId, loadWorkflow, workflowId]);

  function updateForm(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function toggleVariant(variantId) {
    setForm((current) => ({
      ...current,
      variantIds: current.variantIds.includes(variantId)
        ? current.variantIds.filter((id) => id !== variantId)
        : [...current.variantIds, variantId],
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
      setSearchParams({ workflowId: getWorkflowId(response) });
      setMessage('Inventory analysis workflow started.');
      await loadWorkflowList();
    } catch (err) {
      setError(normalizeAgentApiError(err));
    } finally {
      setIsStarting(false);
    }
  }

  function handleLoadWorkflow(event) {
    event.preventDefault();
    const id = workflowIdInput.trim();
    if (id === linkedWorkflowId) loadWorkflow(id);
    else setSearchParams({ workflowId: id });
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
        inventoryCount: normalizeInventoryItems(inventoryResponse).length,
        affectedVariantIds,
        stockHistoryCount: historyResults.reduce((total, [, history]) => total + normalizeStockHistoryItems(history).length, 0),
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

    if (action === 'approve') {
      const restocks = recommendations.filter((item) => getRecommendationAction(item).toUpperCase() === 'RESTOCK');
      const units = restocks.reduce((total, item) => {
        const quantity = Number(getRecommendationProposedQuantity(item));
        return total + (Number.isFinite(quantity) ? quantity : 0);
      }, 0);
      if (!window.confirm(`Approve this workflow? It may add ${units} units across ${restocks.length} ${restocks.length === 1 ? 'variant' : 'variants'} immediately.`)) return;
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
      await loadWorkflowList();
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
      <Alert>Review every recommendation before approval. Stock changes happen only after a staff member approves the workflow.</Alert>

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

          <div className="agent-variant-picker">
            <label>Focus on products (optional)
              <input onChange={(event) => setVariantSearch(event.target.value)} placeholder="Search products or SKUs" type="search" value={variantSearch} />
            </label>
            <p>{form.variantIds.length ? `${form.variantIds.length} selected` : 'No selection analyzes the full inventory.'}</p>
            <ApiErrorAlert message={inventoryLoadError} onRetry={loadInventoryOptions} />
            <div aria-label="Variants to analyze" className="agent-variant-options" role="group">
              {matchingVariants.map((item) => {
                const variantId = String(getVariantId(item));
                return <label key={variantId}><input aria-label={`${getProductName(item)} ${getSku(item)}`} checked={form.variantIds.includes(variantId)} onChange={() => toggleVariant(variantId)} type="checkbox" /><span><strong>{getProductName(item)}</strong><small>{getSku(item)}</small></span></label>;
              })}
              {!inventoryLoadError && !matchingVariants.length && <p>No matching variants. The full inventory can still be analyzed.</p>}
            </div>
          </div>

          <div className="form-actions">
            <button disabled={isStarting} type="submit">
              {isStarting ? 'Starting...' : 'Start workflow'}
            </button>
          </div>
        </form>
      </section>

      <section className="panel" aria-label="Recent inventory workflows">
        <div className="panel__header"><div><h2>Analysis workflows</h2><p>Open a pending analysis to review its recommendation and decision history.</p></div></div>
        <div className="toolbar"><div className="toolbar__filters"><select aria-label="Filter workflows by status" onChange={(event) => { setWorkflowListStatus(event.target.value); setWorkflowListPage(1); }} value={workflowListStatus}><option value="">All statuses</option>{INVENTORY_AGENT_STATUSES.map((item) => <option key={item} value={item}>{item.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</select></div><button className="button button-secondary" disabled={workflowListLoading} onClick={loadWorkflowList} type="button">Refresh list</button></div>
        <ApiErrorAlert message={workflowListError} onRetry={loadWorkflowList} />
        {workflowListLoading ? <LoadingState message="Loading analyses..." /> : workflowList.items.length ? <div className="table-card"><table className="data-table"><caption className="sr-only">Inventory agent workflow queue</caption><thead><tr><th>Objective</th><th>Status</th><th>Started</th><th>Action</th></tr></thead><tbody>{workflowList.items.map((item) => <tr key={item.workflowId}><td>{item.objective}</td><td><span className={`status-pill ${getStatusClass(item.status)}`}>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span></td><td>{item.startedAt ? new Date(item.startedAt).toLocaleString() : '—'}</td><td><button className="button-secondary" onClick={() => { setWorkflowIdInput(item.workflowId); setSearchParams({ workflowId: item.workflowId }); }} type="button">Open workflow</button></td></tr>)}</tbody></table></div> : !workflowListError && <div className="empty-state">No workflows match this status. Start an analysis or choose another status.</div>}
        <nav className="pagination-bar" aria-label="Workflow pagination"><button className="button-secondary" disabled={workflowListPage <= 1 || workflowListLoading} onClick={() => setWorkflowListPage((page) => page - 1)} type="button">Previous</button><span>Page {workflowList.page} of {workflowList.totalPages} ({workflowList.totalItems} workflows)</span><button className="button-secondary" disabled={workflowListPage >= workflowList.totalPages || workflowListLoading} onClick={() => setWorkflowListPage((page) => page + 1)} type="button">Next</button></nav>
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
                <dt>Started</dt>
                <dd>{workflow.startedAt ? new Date(workflow.startedAt).toLocaleString() : '—'}</dd>
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

          {status === 'AwaitingApproval' && (
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

          {postApprovalRefresh && <section className="panel" aria-label="Inventory refresh result">
            <h2>Inventory after review</h2>
            {postApprovalRefresh.refreshError ? <ApiErrorAlert message={postApprovalRefresh.refreshError} /> : <>
              <p>Refreshed {postApprovalRefresh.inventoryCount} stock records and {postApprovalRefresh.stockHistoryCount} movement records for {postApprovalRefresh.affectedVariantIds.length} affected variants.</p>
              <div className="form-actions"><Link className="button button-secondary" to="/inventory">Open inventory</Link>{postApprovalRefresh.affectedVariantIds[0] && <Link className="button button-secondary" to={`/inventory/history?variantId=${postApprovalRefresh.affectedVariantIds[0]}`}>View stock history</Link>}</div>
            </>}
          </section>}
        </>
      ) : (
        <div className="empty-state">
          Start a new inventory analysis workflow or load an existing workflow ID to view persisted state.
        </div>
      )}
    </PageShell>
  );
}
