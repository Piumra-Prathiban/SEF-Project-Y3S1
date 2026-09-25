import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import { getAgentWorkflows } from '../../../services/agentService';
import AgentWorkflowListPage from './AgentWorkflowListPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/agentService', () => ({ getAgentWorkflows: vi.fn() }));

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/agent']}>
      <AgentWorkflowListPage />
    </MemoryRouter>
  );
}

describe('AgentWorkflowListPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getAgentWorkflows).mockReset();
  });

  it('lists workflows with status and timestamps', async () => {
    vi.mocked(getAgentWorkflows).mockResolvedValue([
      {
        workflowId: 'wf-1',
        objective: 'Find products with declining sales and recommend suitable promotions.',
        status: 3,
        createdAt: '2026-10-15T00:00:00Z',
        completedAt: null,
      },
    ]);

    renderPage();

    const link = await screen.findByRole('link', {
      name: 'Find products with declining sales and recommend suitable promotions.',
    });
    expect(link).toHaveAttribute('href', '/marketing/agent/wf-1');
    expect(screen.getByText('Awaiting approval')).toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    vi.mocked(getAgentWorkflows).mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText('No agent runs yet.')).toBeInTheDocument();
  });

  it('shows an error state', async () => {
    vi.mocked(getAgentWorkflows).mockRejectedValue({ status: 500, message: 'Agent service unavailable.' });

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Agent service unavailable.');
  });
});
