import { useCallback, useState } from 'react';
import { useParams } from 'react-router-dom';
import Badge from '../../components/Badge';
import ConfirmAction from '../../components/ConfirmAction';
import FormField from '../../components/FormField';
import PageHeader from '../../components/PageHeader';
import { Alert, ErrorState, LoadingState } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { useAsync } from '../../hooks/useAsync';
import {
  approveFulfilmentAgentWorkflow,
  getFulfilmentAgentWorkflow,
  rejectFulfilmentAgentWorkflow,
  reviseFulfilmentAgentWorkflow,
} from '../../services/fulfilmentAgentService';
import { toErrorMessage } from '../../utils/apiErrors';
import {
  approvalStatusInfo,
  buildRevisionPayload,
  EMPTY_REVISION_FORM,
  formatTimestamp,
  getPendingApproval,
  isAwaitingApproval,
  resolutionOf,
  stepStatusInfo,
  targetLabel,
  toolStatusInfo,
  validateRevisionForm,
  workflowStatusInfo,
} from './fulfilmentAgentUtils';

export default function FulfilmentAgentDetailPage() {
  const { id } = useParams();
  const { token } = useAuth();
  const [refreshKey, setRefreshKey] = useState(0);
  const [message, setMessage] = useState({ tone: 'success', text: '' });
  const [busy, setBusy] = useState(false);
  const [comment, setComment] = useState('');
  const [revising, setRevising] = useState(false);
  const [revisionForm, setRevisionForm] = useState(EMPTY_REVISION_FORM);
  const [revisionErrors, setRevisionErrors] = useState({});

  const loader = useCallback(() => getFulfilmentAgentWorkflow(token, id), [token, id]);
  const { data: workflow, error, loading, reload } = useAsync(loader, refreshKey);

  function refreshAfterAction() {
    setRefreshKey((key) => key + 1);
  }

  async function handleApprove() {
    setBusy(true);
    setMessage({ tone: 'success', text: '' });

    try {
      await approveFulfilmentAgentWorkflow(token, id, comment);
      setComment('');
      setMessage({ tone: 'success', text: 'Approved. The agent is executing the action.' });
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
      await rejectFulfilmentAgentWorkflow(token, id, comment);
      setComment('');
      setMessage({ tone: 'success', text: 'Resolution rejected.' });
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusy(false);
      refreshAfterAction();
    }
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
      await reviseFulfilmentAgentWorkflow(token, id, buildRevisionPayload(revisionForm).comment);
      setRevisionForm(EMPTY_REVISION_FORM);
      setRevising(false);
      setMessage({ tone: 'success', text: 'Revision requested. A new resolution has been drafted.' });
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
  const resolution = resolutionOf(workflow);
  const pendingApproval = getPendingApproval(workflow);

  return (
    <>
      <PageHeader
        title={workflow.objective}
        backTo="/fulfilment-agent"
        backLabel="All workflows"
        actions={
          <button type="button" className="button button-secondary" onClick={reload}>
            Refresh
          </button>
        }
      />

      <div className="button-row" data-testid="workflow-status" style={{ marginBottom: '1rem' }}>
        <Badge tone={status.tone}>{status.label}</Badge>
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

      {message.text && (
        <Alert tone={message.tone} onDismiss={() => setMessage({ tone: 'success', text: '' })}>
          {message.text}
        </Alert>
      )}

      {resolution && (
        <section aria-labelledby="agent-resolution">
          <h2 id="agent-resolution">Proposed resolution</h2>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Proposed resolution</caption>
              <tbody>
                <tr>
                  <th scope="row">Target</th>
                  <td>{targetLabel(resolution)}</td>
                </tr>
                <tr>
                  <th scope="row">Current status</th>
                  <td>{resolution.evidence?.currentStatus ?? '—'}</td>
                </tr>
                <tr>
                  <th scope="row">Proposed action</th>
                  <td><strong>{resolution.action}</strong></td>
                </tr>
                <tr>
                  <th scope="row">Rationale</th>
                  <td>{resolution.rationale}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      )}

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
          <h2 id="agent-review">Review this resolution</h2>

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
                <button type="button" className="button" onClick={handleApprove} disabled={busy}>
                  {busy ? 'Working…' : 'Approve'}
                </button>
                <ConfirmAction
                  label="Reject"
                  question="Reject this resolution?"
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
                    onChange={(e) => setRevisionForm((current) => ({ ...current, comment: e.target.value }))}
                  />
                )}
              </FormField>

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
