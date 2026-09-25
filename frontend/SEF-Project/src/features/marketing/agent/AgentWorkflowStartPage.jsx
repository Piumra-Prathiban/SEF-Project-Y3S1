import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import FormField from '../../../components/FormField';
import PageHeader from '../../../components/PageHeader';
import { Alert } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { startAgentWorkflow } from '../../../services/agentService';
import { toErrorMessage } from '../../../utils/apiErrors';
import { AGENT_FOCUS_OPTIONS } from './agentConstants';
import { buildStartPayload, EMPTY_START_FORM, validateStartForm } from './agentUtils';

export default function AgentWorkflowStartPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(EMPTY_START_FORM);
  const [errors, setErrors] = useState({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setFormError('');

    const clientErrors = validateStartForm(form);
    setErrors(clientErrors);

    if (Object.keys(clientErrors).length > 0) {
      setFormError('Please correct the highlighted fields.');
      return;
    }

    setSubmitting(true);

    try {
      const workflow = await startAgentWorkflow(token, buildStartPayload(form));
      navigate(`/marketing/agent/${workflow.workflowId}`, {
        state: { flash: 'Workflow started.' },
      });
    } catch (error) {
      setFormError(toErrorMessage(error));
      setSubmitting(false);
    }
  }

  return (
    <>
      <PageHeader
        title="Start a promotion agent run"
        backTo="/marketing/agent"
        backLabel="All workflows"
      />

      <form className="form" onSubmit={handleSubmit} noValidate>
        <Alert tone="danger">{formError}</Alert>

        <FormField id="objective" label="Objective" required error={errors.objective}>
          {(props) => (
            <textarea
              {...props}
              rows={2}
              maxLength={500}
              value={form.objective}
              onChange={(e) => update('objective', e.target.value)}
            />
          )}
        </FormField>

        <div className="form-grid">
          <FormField id="focus" label="Focus" required>
            {(props) => (
              <select {...props} value={form.focus} onChange={(e) => update('focus', e.target.value)}>
                {AGENT_FOCUS_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            )}
          </FormField>

          <FormField
            id="analysisDays"
            label="Analysis window (days)"
            required
            hint="Compared with the same number of days before it."
            error={errors.analysisDays}
          >
            {(props) => (
              <input
                {...props}
                type="number"
                min={7}
                max={90}
                value={form.analysisDays}
                onChange={(e) => update('analysisDays', e.target.value)}
              />
            )}
          </FormField>

          <FormField
            id="maxProposals"
            label="Maximum proposals"
            required
            error={errors.maxProposals}
          >
            {(props) => (
              <input
                {...props}
                type="number"
                min={1}
                max={10}
                value={form.maxProposals}
                onChange={(e) => update('maxProposals', e.target.value)}
              />
            )}
          </FormField>

          <FormField
            id="maxDiscountPercent"
            label="Maximum discount (%)"
            required
            error={errors.maxDiscountPercent}
          >
            {(props) => (
              <input
                {...props}
                type="number"
                min={1}
                max={50}
                value={form.maxDiscountPercent}
                onChange={(e) => update('maxDiscountPercent', e.target.value)}
              />
            )}
          </FormField>
        </div>

        <div className="button-row form-actions">
          <button type="submit" className="button" disabled={submitting}>
            {submitting ? 'Starting…' : 'Start run'}
          </button>
        </div>
      </form>
    </>
  );
}
