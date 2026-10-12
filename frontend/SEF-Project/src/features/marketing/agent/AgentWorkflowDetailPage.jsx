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
  salesVelocityItems,
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
  const [evidenceTab, setEvidenceTab] = useState('Sales velocity');

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

  const velocityRows = proposals.map((proposal) => ({
    proposal,
    velocity: salesVelocityForProduct(workflow, proposal.productId),
  }));
  const checksTotal = workflow.validationResults.length;
  const checksPassed = workflow.validationResults.filter((r) => r.isValid).length;
  const toolsTotal = workflow.toolExecutions.length;
  const toolsOk = workflow.toolExecutions.filter((t) => t.status === 1).length;
  const skippedNames = skippedProductNames(workflow, proposals);
  const awaiting = Boolean(pendingApproval && isAwaitingApproval(workflow));

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

      <div className="run-meta">
        <span className="run-meta-badges" data-testid="workflow-status">
          <Badge tone={status.tone}>{status.label}</Badge>
          {workflow.impactLevel !== null && workflow.impactLevel !== undefined && (
            <Badge tone={impactLevelInfo(workflow.impactLevel).tone}>
              {impactLevelInfo(workflow.impactLevel).label}
            </Badge>
          )}
        </span>
        <span className="muted">Started <time>{formatTimestamp(workflow.startedAt)}</time></span>
        {workflow.completedAt && (
          <span className="muted">Completed <time>{formatTimestamp(workflow.completedAt)}</time></span>
        )}
        <span className="muted">ID <code title={workflow.workflowId}>{shortId(workflow.workflowId)}</code></span>
        {workflow.finalOutcome && <span className="muted">Outcome: {workflow.finalOutcome}</span>}
      </div>

      <Alert tone={message.tone} onDismiss={() => setMessage({ tone: 'success', text: '' })}>
        {message.text}
      </Alert>

      <Stepper steps={workflow.steps} />

      <div className="run-tiles">
        <RunTile label="Proposals" value={proposals.length} />
        <RunTile label="Checks passed" value={checksTotal ? `${checksPassed} / ${checksTotal}` : '—'} />
        <RunTile label="Tools succeeded" value={toolsTotal ? `${toolsOk} / ${toolsTotal}` : '—'} />
        <RunTile label="Discount" value={discountSummary(proposals)} />
      </div>

      {proposals.length > 0 && (
        <section aria-labelledby="agent-proposals" className="run-card">
          <div className="run-card-head">
            <h2 id="agent-proposals">Proposed promotions</h2>
            <span className="muted">{dateRange(proposals)}</span>
          </div>
          <div className="proposal-grid">
            {proposals.map((proposal) => {
              const variants = pricingForProduct(workflow, proposal.productId)?.variants ?? [];
              const conflicts = promotions.some((p) => promotionTargetsProduct(p, proposal.productId));

              return (
                <article className="proposal-card" key={proposal.productId}>
                  <header>
                    <h3>{proposal.productName}</h3>
                    <Badge tone="success">{formatOffer(proposal.promotionType, proposal.discountValue)}</Badge>
                  </header>
                  {variants.length === 0 ? (
                    <p className="muted">Not priced</p>
                  ) : (
                    variants.map((v) => (
                      <div className="proposal-price" key={v.productVariantId}>
                        <span className="proposal-sku">{v.sku}</span>
                        <span className="proposal-new">{formatMoney(v.finalPrice)}</span>
                        <s className="proposal-old">{formatMoney(v.originalPrice)}</s>
                      </div>
                    ))
                  )}
                  <p className="muted proposal-note">
                    {proposalNote(workflow, proposal, conflicts)}
                  </p>
                </article>
              );
            })}
          </div>
          {skippedNames.length > 0 && (
            <p className="muted">
              Skipped: {skippedNames.join(', ')} (already have live promotions).
            </p>
          )}
        </section>
      )}

      {awaiting && (
        <section aria-labelledby="agent-review" className="run-card review-panel">
          <div className="run-card-head">
            <div>
              <h2 id="agent-review">Review this proposal</h2>
              <p className="muted">
                Requested {formatTimestamp(pendingApproval.requestedAt)} ·{' '}
                {workflow.impactLevel === 1 ? 'Administrator' : 'Staff or Administrator'}
              </p>
            </div>
            <Badge tone={approvalStatusInfo(pendingApproval.status).tone}>
              {approvalStatusInfo(pendingApproval.status).label}
            </Badge>
          </div>

          {!approval.allowed && <Alert tone="info">{approval.reason}</Alert>}

          {!revising && (
            <>
              <FormField id="review-comment" label="Comment (optional)">
                {(props) => (
                  <textarea
                    {...props}
                    rows={2}
                    maxLength={1000}
                    placeholder="Add a comment (optional)"
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

      {workflow.errors.length > 0 && (
        <section aria-labelledby="agent-errors" className="run-card">
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
        <section aria-labelledby="agent-created" className="run-card">
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

      {proposals.length > 0 && (
        <section aria-labelledby="agent-evidence" className="run-card">
          <h2 id="agent-evidence">Evidence</h2>
          <div className="pill-tabs" role="tablist" aria-label="Evidence">
            {EVIDENCE_TABS.map((tab) => (
              <button
                key={tab}
                type="button"
                role="tab"
                aria-selected={evidenceTab === tab}
                className={evidenceTab === tab ? 'pill-tab pill-tab-active' : 'pill-tab'}
                onClick={() => setEvidenceTab(tab)}
              >
                {tab}
              </button>
            ))}
          </div>

          {evidenceTab === 'Sales velocity' && (
            <section aria-label="Sales velocity" className="table-wrap">
              <table>
                <caption className="visually-hidden">Sales velocity of proposed products</caption>
                <thead>
                  <tr>
                    <th scope="col">Product</th>
                    <th scope="col">Units sold</th>
                    <th scope="col">Previous</th>
                    <th scope="col">Per day</th>
                    <th scope="col">Trend</th>
                  </tr>
                </thead>
                <tbody>
                  {velocityRows.map(({ proposal, velocity }) => (
                    <tr key={proposal.productId}>
                      <td>{proposal.productName}</td>
                      <td>{velocity?.unitsSold ?? '—'}</td>
                      <td>{velocity?.previousUnitsSold ?? '—'}</td>
                      <td>{velocity?.unitsPerDay ?? '—'}</td>
                      <td>
                        {velocity ? (
                          <Badge tone={trendTone(velocity.trend)}>{formatTrend(velocity.trend)}</Badge>
                        ) : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          {evidenceTab === 'Inventory' && (
            <section aria-label="Inventory" className="table-wrap">
              <table>
                <caption className="visually-hidden">Inventory of proposed products</caption>
                <thead>
                  <tr>
                    <th scope="col">Product</th>
                    <th scope="col">SKU</th>
                    <th scope="col">Available</th>
                    <th scope="col">Reorder level</th>
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
                              <td>{variant.availableQuantity}</td>
                              <td>{variant.reorderLevel}</td>
                              <td><Badge tone={stock.tone}>{stock.label}</Badge></td>
                            </tr>
                          );
                        });
                  })}
                </tbody>
              </table>
            </section>
          )}

          {evidenceTab === 'Existing promotions' && (
            <section aria-label="Existing promotion information">
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
          )}
        </section>
      )}

      <Collapsible
        title="Validation"
        badge={
          checksTotal > 0 && (
            <Badge tone={checksPassed === checksTotal ? 'success' : 'danger'}>
              {checksPassed} / {checksTotal} passed
            </Badge>
          )
        }
      >
        {checksTotal === 0 ? (
          <p className="muted">No validation has run yet.</p>
        ) : (
          groupValidation(workflow.validationResults, proposals).map((group) => (
            <div className="validation-group" key={group.name}>
              <h3>{group.name}</h3>
              <ul className="plain-list">
                {group.results.map((result, index) => (
                  <li key={index}>
                    <Badge tone={result.isValid ? 'success' : 'danger'}>
                      {result.isValid ? 'Pass' : 'Fail'}
                    </Badge>{' '}
                    <strong>{result.validatorName}</strong> — {result.message}
                  </li>
                ))}
              </ul>
            </div>
          ))
        )}
      </Collapsible>

      <Collapsible title="Agent execution" badge={<Badge tone="muted">{workflow.steps.length} steps</Badge>}>
        {workflow.plan.length > 0 && (
          <>
            <h3>Plan</h3>
            <ol>
              {workflow.plan.map((step) => <li key={step}>{step}</li>)}
            </ol>
          </>
        )}
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
      </Collapsible>

      <Collapsible
        title="Tool calls"
        badge={toolsTotal > 0 && (
          <Badge tone={toolsOk === toolsTotal ? 'success' : 'warning'}>{toolsOk} / {toolsTotal} success</Badge>
        )}
      >
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
                      {execution.errorMessage ? (
                        <p className="field-error">{execution.errorMessage}</p>
                      ) : '—'}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Collapsible>

      <section aria-labelledby="agent-history" className="run-card">
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
    </>
  );
}

const EVIDENCE_TABS = ['Sales velocity', 'Inventory', 'Existing promotions'];

function Stepper({ steps }) {
  const ordered = [...steps].sort((a, b) => a.stepOrder - b.stepOrder);
  const activeIndex = ordered.findIndex((s) => s.status !== 2);

  return (
    <ol className="run-stepper" aria-label="Workflow progress">
      {ordered.map((step, index) => {
        const done = step.status === 2;
        const failed = step.status === 3;
        let state = 'pending';

        if (failed) state = 'failed';
        else if (done) state = 'done';
        else if (index === activeIndex) state = 'active';

        return (
          <li key={step.stepOrder} className={`run-step run-step-${state}`}>
            <span className="run-step-dot" aria-hidden="true">
              {done ? '✓' : failed ? '!' : step.stepOrder}
            </span>
            <span className="run-step-label">{step.title}</span>
          </li>
        );
      })}
    </ol>
  );
}

function RunTile({ label, value }) {
  return (
    <div className="run-tile">
      <span className="run-tile-label">{label}</span>
      <span className="run-tile-value">{value}</span>
    </div>
  );
}

function Collapsible({ title, badge, children }) {
  const [open, setOpen] = useState(false);

  return (
    <section className="run-card collapse-card" aria-label={title}>
      <button
        type="button"
        className="collapse-head"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="collapse-title">{title}</span>
        {badge}
        <span className="collapse-toggle">
          {open ? 'Hide' : 'Show'}
          <span className={open ? 'chevron chevron-up' : 'chevron'} aria-hidden="true" />
        </span>
      </button>
      {open && <div className="collapse-body">{children}</div>}
    </section>
  );
}

function shortId(id) {
  return id && id.length > 16 ? `${id.slice(0, 8)}…${id.slice(-4)}` : id;
}

function trendTone(trend) {
  if (trend === 'Falling') return 'danger';
  if (trend === 'Rising') return 'success';
  return 'muted';
}

function daysBetween(start, end) {
  return Math.max(1, Math.round((new Date(end) - new Date(start)) / 86400000));
}

function discountSummary(proposals) {
  if (proposals.length === 0) return '—';

  const first = proposals[0];
  const offer = formatOffer(first.promotionType, first.discountValue).replace(/ off$/, '');

  return `${offer} · ${daysBetween(first.startDate, first.endDate)} days`;
}

const shortDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' });
const longDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });

function dateRange(proposals) {
  const starts = proposals.map((p) => new Date(p.startDate));
  const ends = proposals.map((p) => new Date(p.endDate));

  return `${shortDate.format(new Date(Math.min(...starts)))} – ${longDate.format(new Date(Math.max(...ends)))}`;
}

function proposalNote(workflow, proposal, hasLivePromotion) {
  const evidence = proposal.evidence;

  if (!evidence || evidence.previousUnitsSold === undefined) {
    return proposal.rationale;
  }

  const { unitsSold, previousUnitsSold, availableQuantity } = evidence;
  const change = previousUnitsSold > 0
    ? ` (${Math.round(((unitsSold - previousUnitsSold) / previousUnitsSold) * 100)}%)`
    : '';
  const parts = [`Sales ${previousUnitsSold} → ${unitsSold}${change}`];

  if (availableQuantity !== undefined) {
    const reorder = inventoryForProduct(workflow, proposal.productId)[0]?.reorderLevel;
    const reorderText = reorder !== undefined ? ` (reorder at ${reorder})` : '';

    parts.push(`Stock ${availableQuantity} units${reorderText}`);
  }

  parts.push(hasLivePromotion ? 'Has an active promotion' : 'No active promotion');

  return parts.join(' · ');
}

// Falling products that were not proposed because a live promotion already
// targets them. Only listed when the tool results name them.
function skippedProductNames(workflow, proposals) {
  const proposed = new Set(proposals.map((p) => p.productId));
  const live = activePromotionItems(workflow);

  return salesVelocityItems(workflow)
    .filter((item) => item.productName
      && !proposed.has(item.productId)
      && live.some((promotion) => promotionTargetsProduct(promotion, item.productId)))
    .map((item) => item.productName);
}

// Validator messages are prefixed with the product name ("Name: message");
// group by that prefix when it matches a proposed product.
function groupValidation(results, proposals) {
  const names = proposals.map((p) => p.productName);
  const groups = new Map();

  results.forEach((result) => {
    const name = names.find((n) => result.message?.startsWith(`${n}:`)) ?? 'General';

    if (!groups.has(name)) groups.set(name, []);
    groups.get(name).push(result);
  });

  return [...groups].map(([name, grouped]) => ({ name, results: grouped }));
}
