import { useCallback, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import Badge from '../../../components/Badge';
import ConfirmAction from '../../../components/ConfirmAction';
import FormField from '../../../components/FormField';
import PageHeader from '../../../components/PageHeader';
import { Alert, ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import {
  approveAgentWorkflow,
  getAgentWorkflow,
  rejectAgentWorkflow,
  reviseAgentWorkflow,
} from '../../../services/agentService';
import { toErrorMessage } from '../../../utils/apiErrors';
import { formatMoney } from '../marketingUtils';
import {
  activePromotionItems,
  approvalStatusInfo,
  buildRevisionPayload,
  canApprove,
  EMPTY_REVISION_FORM,
  formatOffer,
  formatTimestamp,
  formatTrend,
  getPendingApproval,
  impactLevelInfo,
  inventoryForProduct,
  isAwaitingApproval,
  labelForCreatedPromotion,
  pricingForProduct,
  promotionTargetsProduct,
  salesVelocityForProduct,
  stepStatusInfo,
  stockStatusInfo,
  toolStatusInfo,
  validateRevisionForm,
  workflowStatusInfo,
} from './agentUtils';

export default function AgentWorkflowDetailPage() {
  const { id } = useParams();
  const { token, user } = useAuth();
  const [refreshKey, setRefreshKey] = useState(0);
  const [message, setMessage] = useState({ tone: 'success', text: '' });
  const [busy, setBusy] = useState(false);
  const [comment, setComment] = useState('');
  const [revising, setRevising] = useState(false);
  const [revisionForm, setRevisionForm] = useState(EMPTY_REVISION_FORM);
  const [revisionErrors, setRevisionErrors] = useState({});

  const loader = useCallback(() => getAgentWorkflow(token, id), [token, id]);
  const { data: workflow, error, loading, reload } = useAsync(loader, refreshKey);

  function refreshAfterAction() {
    // The server is authoritative: reload rather than trust the local
    // decision, so a concurrent approval/rejection is reflected (stale
    // workflow handling).
    setRefreshKey((key) => key + 1);
  }

  async function handleApprove() {
    setBusy(true);
    setMessage({ tone: 'success', text: '' });

    try {
      await approveAgentWorkflow(token, id, comment);
      setComment('');
      setMessage({ tone: 'success', text: 'Approved. The agent is creating the promotion(s).' });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusy(false);
      refreshAfterAction();
    }
  }

  async function handleReject() {
    setBusy(true);
    setMessage({ tone: 'success', text: '' });

    try {
      await rejectAgentWorkflow(token, id, comment);
      setComment('');
      setMessage({ tone: 'success', text: 'Proposal rejected.' });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusy(false);
      refreshAfterAction();
    }
  }

  function updateRevision(field, value) {
    setRevisionForm((current) => ({ ...current, [field]: value }));
    setRevisionErrors((current) => ({ ...current, [field]: undefined }));
  }

  function toggleExcludedProduct(productId) {
    setRevisionForm((current) => ({
      ...current,
      excludeProductIds: current.excludeProductIds.includes(productId)
        ? current.excludeProductIds.filter((id_) => id_ !== productId)
        : [...current.excludeProductIds, productId],
    }));
  }

  async function handleRevise(event) {
    event.preventDefault();
    setMessage({ tone: 'success', text: '' });

    const errors = validateRevisionForm(revisionForm);
    setRevisionErrors(errors);

    if (Object.keys(errors).length > 0) {
      return;
    }

    setBusy(true);

    try {
      await reviseAgentWorkflow(token, id, buildRevisionPayload(revisionForm));
      setRevisionForm(EMPTY_REVISION_FORM);
      setRevising(false);
      setMessage({ tone: 'success', text: 'Revision requested. A new proposal has been drafted.' });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusy(false);
      refreshAfterAction();
    }
  }

  if (loading) {
    return <LoadingState label="Loading workflow…" />;
  }

  if (error) {
    return <ErrorState error={error} onRetry={reload} />;
  }

  const status = workflowStatusInfo(workflow.status);
  const pendingApproval = getPendingApproval(workflow);
  const approval = canApprove(workflow, user?.role);
  const proposals = workflow.proposal?.proposals ?? [];
  const promotions = activePromotionItems(workflow);

  return (
    <>
      <PageHeader
        title={workflow.objective}
        backTo="/marketing/agent"
        backLabel="All workflows"
        actions={
          <button type="button" className="button button-secondary" onClick={reload}>
            Refresh
          </button>
        }
      />

      <div className="button-row" data-testid="workflow-status" style={{ marginBottom: '1rem' }}>
        <Badge tone={status.tone}>{status.label}</Badge>
        {workflow.impactLevel !== null && workflow.impactLevel !== undefined && (
          <Badge tone={impactLevelInfo(workflow.impactLevel).tone}>
            {impactLevelInfo(workflow.impactLevel).label}
          </Badge>
        )}
      </div>

      <dl className="details">
        <dt>Workflow ID</dt>
        <dd><code>{workflow.workflowId}</code></dd>
        <dt>Started</dt>
        <dd>{formatTimestamp(workflow.startedAt)}</dd>
        <dt>Completed</dt>
        <dd>{formatTimestamp(workflow.completedAt)}</dd>
        {workflow.finalOutcome && (
          <>
            <dt>Outcome</dt>
            <dd>{workflow.finalOutcome}</dd>
          </>
        )}
      </dl>

      <Alert tone={message.tone} onDismiss={() => setMessage({ tone: 'success', text: '' })}>
        {message.text}
      </Alert>

      <section aria-labelledby="agent-plan">
        <h2 id="agent-plan">Plan</h2>
        {workflow.plan.length === 0 ? (
          <p className="muted">No plan recorded.</p>
        ) : (
          <ol>
            {workflow.plan.map((step) => <li key={step}>{step}</li>)}
          </ol>
        )}
      </section>

      <section aria-labelledby="agent-steps">
        <h2 id="agent-steps">Agent execution summary</h2>
        <div className="table-wrap">
          <table>
            <caption className="visually-hidden">Execution steps</caption>
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">Agent</th>
                <th scope="col">Step</th>
                <th scope="col">Status</th>
                <th scope="col">Summary</th>
              </tr>
            </thead>
            <tbody>
              {workflow.steps.map((step) => {
                const info = stepStatusInfo(step.status);

                return (
                  <tr key={step.stepOrder}>
                    <td>{step.stepOrder}</td>
                    <td>{step.agentName}</td>
                    <td>{step.title}</td>
                    <td><Badge tone={info.tone}>{info.label}</Badge></td>
                    <td>{step.summary ?? '—'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      {proposals.length > 0 && (
        <section aria-labelledby="agent-proposals">
          <h2 id="agent-proposals">Proposed promotions</h2>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Proposed promotions</caption>
              <thead>
                <tr>
                  <th scope="col">Product</th>
                  <th scope="col">Proposed discount</th>
                  <th scope="col">Dates</th>
                  <th scope="col">Current price</th>
                  <th scope="col">Proposed price</th>
                  <th scope="col">Rationale</th>
                </tr>
              </thead>
              <tbody>
                {proposals.map((proposal) => {
                  const pricing = pricingForProduct(workflow, proposal.productId);
                  const variants = pricing?.variants ?? [];

                  return (
                    <tr key={proposal.productId}>
                      <td>{proposal.productName}</td>
                      <td>{formatOffer(proposal.promotionType, proposal.discountValue)}</td>
                      <td>
                        {new Date(proposal.startDate).toLocaleDateString()} –{' '}
                        {new Date(proposal.endDate).toLocaleDateString()}
                      </td>
                      <td>
                        {variants.length === 0
                          ? 'Not priced'
                          : variants.map((v) => (
                              <div key={v.productVariantId}>{v.sku}: {formatMoney(v.originalPrice)}</div>
                            ))}
                      </td>
                      <td>
                        {variants.length === 0
                          ? '—'
                          : variants.map((v) => (
                              <div key={v.productVariantId}>{v.sku}: {formatMoney(v.finalPrice)}</div>
                            ))}
                      </td>
                      <td>{proposal.rationale}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {proposals.length > 0 && (
        <section aria-labelledby="agent-velocity">
          <h2 id="agent-velocity">Sales velocity</h2>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Sales velocity of proposed products</caption>
              <thead>
                <tr>
                  <th scope="col">Product</th>
                  <th scope="col" className="numeric">Units sold</th>
                  <th scope="col" className="numeric">Previous period</th>
                  <th scope="col" className="numeric">Units/day</th>
                  <th scope="col" className="numeric">Trend</th>
                </tr>
              </thead>
              <tbody>
                {proposals.map((proposal) => {
                  const velocity = salesVelocityForProduct(workflow, proposal.productId);

                  return (
                    <tr key={proposal.productId}>
                      <td>{proposal.productName}</td>
                      <td className="numeric">{velocity?.unitsSold ?? '—'}</td>
                      <td className="numeric">{velocity?.previousUnitsSold ?? '—'}</td>
                      <td className="numeric">{velocity?.unitsPerDay ?? '—'}</td>
                      <td className="numeric">{velocity ? formatTrend(velocity.trend) : '—'}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {proposals.length > 0 && (
        <section aria-labelledby="agent-inventory">
          <h2 id="agent-inventory">Inventory</h2>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Inventory of proposed products</caption>
              <thead>
                <tr>
                  <th scope="col">Product</th>
                  <th scope="col">SKU</th>
                  <th scope="col" className="numeric">Available</th>
                  <th scope="col" className="numeric">Reorder level</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                {proposals.flatMap((proposal) => {
                  const variants = inventoryForProduct(workflow, proposal.productId);

                  return variants.length === 0
                    ? [
                        <tr key={proposal.productId}>
                          <td>{proposal.productName}</td>
                          <td colSpan={4}>Not available.</td>
                        </tr>,
                      ]
                    : variants.map((variant) => {
                        const stock = stockStatusInfo(variant.stockStatus);

                        return (
                          <tr key={variant.productVariantId}>
                            <td>{proposal.productName}</td>
                            <td>{variant.sku}</td>
                            <td className="numeric">{variant.availableQuantity}</td>
                            <td className="numeric">{variant.reorderLevel}</td>
                            <td><Badge tone={stock.tone}>{stock.label}</Badge></td>
                          </tr>
                        );
                      });
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <section aria-labelledby="agent-live-promotions">
        <h2 id="agent-live-promotions">Existing promotion information</h2>
        {promotions.length === 0 ? (
          <p className="muted">No promotions are currently live.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Live promotions</caption>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Offer</th>
                  <th scope="col">Dates</th>
                  <th scope="col">Conflict</th>
                </tr>
              </thead>
              <tbody>
                {promotions.map((promotion) => {
                  const conflicts = proposals.some((p) =>
                    promotionTargetsProduct(promotion, p.productId));

                  return (
                    <tr key={promotion.id}>
                      <td>{promotion.name}</td>
                      <td>{formatOffer(promotion.type, promotion.discountValue)}</td>
                      <td>
                        {new Date(promotion.startDate).toLocaleDateString()} –{' '}
                        {new Date(promotion.endDate).toLocaleDateString()}
                      </td>
                      <td>
                        {conflicts ? (
                          <Badge tone="warning">Targets a proposed product</Badge>
                        ) : '—'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section aria-labelledby="agent-validation">
        <h2 id="agent-validation">Validation results</h2>
        {workflow.validationResults.length === 0 ? (
          <p className="muted">No validation has run yet.</p>
        ) : (
          <ul className="plain-list">
            {workflow.validationResults.map((result, index) => (
              <li key={index}>
                <Badge tone={result.isValid ? 'success' : 'danger'}>
                  {result.isValid ? 'Pass' : 'Fail'}
                </Badge>{' '}
                <strong>{result.validatorName}</strong> — {result.message}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="agent-tools">
        <h2 id="agent-tools">Tool execution summary</h2>
        <div className="table-wrap">
          <table>
            <caption className="visually-hidden">Tool executions</caption>
            <thead>
              <tr>
                <th scope="col">Tool</th>
                <th scope="col">Status</th>
                <th scope="col">Started</th>
                <th scope="col">Completed</th>
                <th scope="col">Details</th>
              </tr>
            </thead>
            <tbody>
              {workflow.toolExecutions.map((execution, index) => {
                const info = toolStatusInfo(execution.status);

                return (
                  <tr key={index}>
                    <td>{execution.toolName}</td>
                    <td><Badge tone={info.tone}>{info.label}</Badge></td>
                    <td>{formatTimestamp(execution.startedAt)}</td>
                    <td>{formatTimestamp(execution.completedAt)}</td>
                    <td>
                      {execution.errorMessage && (
                        <p className="field-error">{execution.errorMessage}</p>
                      )}
                      <details>
                        <summary>Raw data</summary>
                        <pre>{JSON.stringify({ arguments: execution.arguments, result: execution.result }, null, 2)}</pre>
                      </details>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      {workflow.errors.length > 0 && (
        <section aria-labelledby="agent-errors">
          <h2 id="agent-errors">Errors</h2>
          <ul className="plain-list">
            {workflow.errors.map((err, index) => (
              <li key={index}>
                <Badge tone="danger">{err.errorType}</Badge> {err.message}{' '}
                <span className="muted">({formatTimestamp(err.occurredAt)})</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {workflow.createdPromotionIds.length > 0 && (
        <section aria-labelledby="agent-created">
          <h2 id="agent-created">Created promotions</h2>
          <ul className="plain-list">
            {workflow.createdPromotionIds.map((promotionId, index) => (
              <li key={promotionId}>
                <Link to={`/marketing/promotions/${promotionId}`}>
                  {labelForCreatedPromotion(workflow, index)}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section aria-labelledby="agent-history">
        <h2 id="agent-history">Review history</h2>
        {workflow.approvals.length === 0 ? (
          <p className="muted">No review has happened yet.</p>
        ) : (
          <ul className="plain-list">
            {workflow.approvals.map((approvalRecord, index) => {
              const info = approvalStatusInfo(approvalRecord.status);

              return (
                <li key={index}>
                  <Badge tone={info.tone}>{info.label}</Badge>{' '}
                  requested {formatTimestamp(approvalRecord.requestedAt)}
                  {approvalRecord.reviewedByUserId && (
                    <>
                      {' '}· reviewed by user {approvalRecord.reviewedByUserId} at{' '}
                      {formatTimestamp(approvalRecord.reviewedAt)}
                    </>
                  )}
                  {approvalRecord.comment && <> — “{approvalRecord.comment}”</>}
                </li>
              );
            })}
          </ul>
        )}
      </section>

      {pendingApproval && isAwaitingApproval(workflow) && (
        <section aria-labelledby="agent-review" className="review-panel">
          <h2 id="agent-review">Review this proposal</h2>

          {!approval.allowed && <Alert tone="info">{approval.reason}</Alert>}

          {!revising && (
            <>
              <FormField id="review-comment" label="Comment (optional)">
                {(props) => (
                  <textarea
                    {...props}
                    rows={2}
                    maxLength={1000}
                    value={comment}
                    onChange={(e) => setComment(e.target.value)}
                  />
                )}
              </FormField>

              <div className="button-row">
                <button
                  type="button"
                  className="button"
                  onClick={handleApprove}
                  disabled={busy || !approval.allowed}
                >
                  {busy ? 'Working…' : 'Approve'}
                </button>
                <ConfirmAction
                  label="Reject"
                  question="Reject this proposal?"
                  confirmLabel="Yes, reject"
                  onConfirm={handleReject}
                  busy={busy}
                />
                <button
                  type="button"
                  className="button button-secondary"
                  onClick={() => setRevising(true)}
                  disabled={busy}
                >
                  Request revision
                </button>
              </div>
            </>
          )}

          {revising && (
            <form className="form" onSubmit={handleRevise} noValidate>
              <FormField
                id="revision-comment"
                label="What should change?"
                required
                error={revisionErrors.comment}
              >
                {(props) => (
                  <textarea
                    {...props}
                    rows={2}
                    maxLength={1000}
                    value={revisionForm.comment}
                    onChange={(e) => updateRevision('comment', e.target.value)}
                  />
                )}
              </FormField>

              <div className="form-grid">
                <FormField
                  id="revision-max-discount"
                  label="New maximum discount (%)"
                  error={revisionErrors.maxDiscountPercent}
                >
                  {(props) => (
                    <input
                      {...props}
                      type="number"
                      min={1}
                      max={50}
                      value={revisionForm.maxDiscountPercent}
                      onChange={(e) => updateRevision('maxDiscountPercent', e.target.value)}
                    />
                  )}
                </FormField>

                <FormField
                  id="revision-max-proposals"
                  label="New maximum proposals"
                  error={revisionErrors.maxProposals}
                >
                  {(props) => (
                    <input
                      {...props}
                      type="number"
                      min={1}
                      max={10}
                      value={revisionForm.maxProposals}
                      onChange={(e) => updateRevision('maxProposals', e.target.value)}
                    />
                  )}
                </FormField>
              </div>

              {proposals.length > 0 && (
                <fieldset className="checkbox-group">
                  <legend>Exclude these products from the next proposal</legend>
                  {proposals.map((proposal) => (
                    <div className="field-inline" key={proposal.productId}>
                      <input
                        id={`exclude-${proposal.productId}`}
                        type="checkbox"
                        checked={revisionForm.excludeProductIds.includes(proposal.productId)}
                        onChange={() => toggleExcludedProduct(proposal.productId)}
                      />
                      <label htmlFor={`exclude-${proposal.productId}`}>{proposal.productName}</label>
                    </div>
                  ))}
                </fieldset>
              )}

              <div className="button-row form-actions">
                <button type="submit" className="button" disabled={busy}>
                  {busy ? 'Submitting…' : 'Submit revision request'}
                </button>
                <button
                  type="button"
                  className="button button-secondary"
                  onClick={() => setRevising(false)}
                  disabled={busy}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
        </section>
      )}
    </>
  );
}
