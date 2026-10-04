import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import Badge from '../../components/Badge';
import PageHeader from '../../components/PageHeader';
import { EmptyState, ErrorState, LoadingState } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { useAsync } from '../../hooks/useAsync';
import { getFulfilmentAgentWorkflows } from '../../services/fulfilmentAgentService';
import { formatTimestamp, workflowStatusInfo } from './fulfilmentAgentUtils';

export default function FulfilmentAgentListPage() {
  const { token } = useAuth();

  const loader = useCallback(() => getFulfilmentAgentWorkflows(token, { limit: 50 }), [token]);
  const { data, error, loading, reload } = useAsync(loader);

  return (
    <>
      <PageHeader
        title="Fulfilment Agent"
        subtitle="The Fulfilment Exception Agent investigates a stuck order and proposes the next permitted action for review."
        actions={<Link className="button" to="/fulfilment-agent/new">Start a new run</Link>}
      />

      {loading && <LoadingState label="Loading workflows…" />}
      {error && <ErrorState error={error} onRetry={reload} />}

      {!loading && !error && data?.length === 0 && (
        <EmptyState title="No agent runs yet.">
          Start a run to have the agent investigate an order and propose a resolution.
        </EmptyState>
      )}

      {!loading && !error && data?.length > 0 && (
        <div className="table-wrap">
          <table>
            <caption className="visually-hidden">Agent workflows</caption>
            <thead>
              <tr>
                <th scope="col">Objective</th>
                <th scope="col">Status</th>
                <th scope="col">Started</th>
                <th scope="col">Completed</th>
              </tr>
            </thead>
            <tbody>
              {data.map((workflow) => {
                const status = workflowStatusInfo(workflow.status);

                return (
                  <tr key={workflow.workflowId}>
                    <td>
                      <Link to={`/fulfilment-agent/${workflow.workflowId}`}>{workflow.objective}</Link>
                    </td>
                    <td><Badge tone={status.tone}>{status.label}</Badge></td>
                    <td>{formatTimestamp(workflow.createdAt)}</td>
                    <td>{formatTimestamp(workflow.completedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
