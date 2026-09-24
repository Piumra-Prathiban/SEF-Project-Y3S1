import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import {
  approveAgentWorkflow,
  getAgentWorkflow,
  rejectAgentWorkflow,
  reviseAgentWorkflow,
} from '../../../services/agentService';
import AgentWorkflowDetailPage from './AgentWorkflowDetailPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/agentService', () => ({
  getAgentWorkflow: vi.fn(),
  approveAgentWorkflow: vi.fn(),
  rejectAgentWorkflow: vi.fn(),
  reviseAgentWorkflow: vi.fn(),
}));

function buildWorkflow(overrides = {}) {
  return {
    workflowId: 'wf-1',
    objective: 'Find products with declining sales and recommend suitable promotions.',
    status: 3, // AwaitingApproval
    plan: ['Retrieve sales velocity (GetSalesVelocity)', 'Draft a structured promotion proposal'],
    impactLevel: 0, // Low
    proposal: {
      schemaVersion: '1.0',
      summary: '1 promotion proposal(s) for declining products.',
      proposals: [
        {
          productId: 'prod-1',
          productName: 'Spaghetti Carbonara',
          promotionType: 'PercentageDiscount',
          discountValue: 15,
          startDate: '2026-10-17T00:00:00Z',
          endDate: '2026-10-31T00:00:00Z',
          rationale: 'Units sold fell from 4 to 3 (25% lower) over the last 30 days.',
          evidence: { unitsSold: 3, previousUnitsSold: 4, availableQuantity: 35 },
        },
      ],
    },
    pricing: [
      {
        productId: 'prod-1',
        variants: [
          { productVariantId: 'var-1', sku: 'PST-CARB-R', originalPrice: 1800, discountAmount: 270, finalPrice: 1530 },
        ],
      },
    ],
    steps: [
      { stepOrder: 1, agentName: 'Inventory & Promotion Agent', title: 'Gather data', status: 2, summary: 'Retrieved data.' },
      { stepOrder: 2, agentName: 'Deterministic Validator', title: 'Validate proposal', status: 2, summary: 'All checks passed.' },
    ],
    toolExecutions: [
      {
        stepOrder: 1,
        toolName: 'GetSalesVelocity',
        status: 1,
        startedAt: '2026-10-16T00:00:00Z',
        completedAt: '2026-10-16T00:00:01Z',
        arguments: { analysisDays: 30 },
        result: {
          items: [
            { productId: 'prod-1', unitsSold: 3, previousUnitsSold: 4, unitsPerDay: 0.1, trend: 'Falling' },
          ],
        },
        errorMessage: null,
      },
      {
        stepOrder: 1,
        toolName: 'GetInventory',
        status: 1,
        startedAt: '2026-10-16T00:00:02Z',
        completedAt: '2026-10-16T00:00:03Z',
        arguments: { productIds: ['prod-1'] },
        result: {
          items: [
            { productId: 'prod-1', productVariantId: 'var-1', sku: 'PST-CARB-R', availableQuantity: 35, reorderLevel: 10, stockStatus: 'InStock' },
          ],
        },
        errorMessage: null,
      },
      {
        stepOrder: 1,
        toolName: 'GetActivePromotions',
        status: 1,
        startedAt: '2026-10-16T00:00:04Z',
        completedAt: '2026-10-16T00:00:05Z',
        arguments: {},
        result: {
          items: [
            { id: 'promo-9', name: 'Pizza 20% Off', type: 'PercentageDiscount', discountValue: 20, productIds: ['prod-9'] },
          ],
        },
        errorMessage: null,
      },
    ],
    validationResults: [
      { stepOrder: 2, validatorName: 'ProductExists', isValid: true, severity: 0, message: 'Spaghetti Carbonara: active product found.' },
      { stepOrder: 2, validatorName: 'SufficientInventory', isValid: true, severity: 0, message: 'Spaghetti Carbonara: 35 available.' },
    ],
    approvals: [{ status: 0, requestedAt: '2026-10-16T00:00:10Z' }],
    errors: [],
    createdPromotionIds: [],
    finalOutcome: null,
    startedAt: '2026-10-15T23:30:00Z',
    completedAt: null,
    ...overrides,
  };
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/agent/wf-1']}>
      <Routes>
        <Route path="/marketing/agent/:id" element={<AgentWorkflowDetailPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('AgentWorkflowDetailPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1', user: { role: 'Staff' } });
    vi.mocked(getAgentWorkflow).mockReset();
    vi.mocked(approveAgentWorkflow).mockReset();
    vi.mocked(rejectAgentWorkflow).mockReset();
    vi.mocked(reviseAgentWorkflow).mockReset();
  });

  it('displays every required piece of workflow information', async () => {
    vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow());

    renderPage();

    expect(
      await screen.findByRole('heading', {
        name: 'Find products with declining sales and recommend suitable promotions.',
      })
    ).toBeInTheDocument();
    expect(screen.getByText('wf-1')).toBeInTheDocument();
    expect(screen.getByText('Awaiting approval')).toBeInTheDocument();
    expect(screen.getByText('Low impact')).toBeInTheDocument();

    // Execution summary.
    expect(screen.getByText('Gather data')).toBeInTheDocument();
    expect(screen.getByText('Retrieved data.')).toBeInTheDocument();

    // Proposed product with current vs proposed price.
    const proposals = screen.getByRole('region', { name: 'Proposed promotions' });
    expect(within(proposals).getByText('15% off')).toBeInTheDocument();
    expect(within(proposals).getByText('PST-CARB-R: LKR 1,800.00')).toBeInTheDocument();
    expect(within(proposals).getByText('PST-CARB-R: LKR 1,530.00')).toBeInTheDocument();

    // Sales velocity.
    const velocity = screen.getByRole('region', { name: 'Sales velocity' });
    expect(within(velocity).getByText('3')).toBeInTheDocument();
    expect(within(velocity).getByText('Falling')).toBeInTheDocument();

    // Inventory.
    const inventory = screen.getByRole('region', { name: 'Inventory' });
    expect(within(inventory).getByText('35')).toBeInTheDocument();
    expect(within(inventory).getByText('In stock')).toBeInTheDocument();

    // Existing promotion information (no conflict here).
    const live = screen.getByRole('region', { name: 'Existing promotion information' });
    expect(within(live).getByText('Pizza 20% Off')).toBeInTheDocument();
    expect(within(live).getByText('—')).toBeInTheDocument();

    // Validation results.
    expect(screen.getByText('SufficientInventory')).toBeInTheDocument();
    expect(screen.getAllByText('Pass')).toHaveLength(2);

    // Tool execution summary.
    const tools = screen.getByRole('region', { name: 'Tool execution summary' });
    expect(within(tools).getByText('GetSalesVelocity')).toBeInTheDocument();
    expect(within(tools).getAllByText('Success')).toHaveLength(3);

    // Timestamps.
    expect(screen.getByText('15 Oct 2026, 23:30 UTC')).toBeInTheDocument();
  });

  it('flags a live promotion that already targets a proposed product', async () => {
    const workflow = buildWorkflow();
    workflow.toolExecutions[2].result.items[0].productIds = ['prod-1'];
    vi.mocked(getAgentWorkflow).mockResolvedValue(workflow);

    renderPage();

    expect(await screen.findByText('Targets a proposed product')).toBeInTheDocument();
  });

  it('shows errors and created promotions when present', async () => {
    vi.mocked(getAgentWorkflow).mockResolvedValue(
      buildWorkflow({
        status: 4,
        errors: [{ errorType: 'ValidationFailed', message: 'Discount too high.', occurredAt: '2026-10-16T01:00:00Z' }],
        createdPromotionIds: ['new-promo-1'],
        finalOutcome: 'Created 1 promotion(s).',
      })
    );

    renderPage();

    expect(await screen.findByText('Discount too high.')).toBeInTheDocument();
    expect(screen.getByText('ValidationFailed')).toBeInTheDocument();
    expect(screen.getByText('Created 1 promotion(s).')).toBeInTheDocument();

    const link = screen.getByRole('link', { name: 'Spaghetti Carbonara' });
    expect(link).toHaveAttribute('href', '/marketing/promotions/new-promo-1');
  });

  it('shows a loading state, then an error state with retry', async () => {
    vi.mocked(getAgentWorkflow)
      .mockRejectedValueOnce({ status: 500, message: 'Could not load the workflow.' })
      .mockResolvedValue(buildWorkflow());

    renderPage();

    expect(screen.getByText('Loading workflow…')).toBeInTheDocument();
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the workflow.');

    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Awaiting approval')).toBeInTheDocument();
  });

  it('hides the review actions once the workflow is no longer awaiting approval', async () => {
    vi.mocked(getAgentWorkflow).mockResolvedValue(
      buildWorkflow({ status: 4, approvals: [{ status: 1, requestedAt: '2026-10-16T00:00:00Z', reviewedByUserId: 7, reviewedAt: '2026-10-16T01:00:00Z' }] })
    );

    renderPage();

    await screen.findByTestId('workflow-status');

    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
    expect(within(screen.getByTestId('workflow-status')).getByText('Completed')).toBeInTheDocument();
    expect(screen.getByText('Approved')).toBeInTheDocument(); // in the review history
  });

  describe('approving', () => {
    it('lets Staff approve a low-impact proposal and persists the comment', async () => {
      const user = userEvent.setup();
      vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow());
      vi.mocked(approveAgentWorkflow).mockResolvedValue(buildWorkflow({ status: 4 }));

      renderPage();
      await screen.findByRole('button', { name: 'Approve' });

      await user.type(screen.getByLabelText('Comment (optional)'), 'Looks good.');
      await user.click(screen.getByRole('button', { name: 'Approve' }));

      await waitFor(() =>
        expect(approveAgentWorkflow).toHaveBeenCalledWith('token-1', 'wf-1', 'Looks good.')
      );
      expect(await screen.findByText(/Approved\. The agent is creating/)).toBeInTheDocument();
      expect(getAgentWorkflow).toHaveBeenCalledTimes(2); // initial load + reload after the action
    });

    it('blocks Staff from approving a high-impact proposal and explains why', async () => {
      vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow({ impactLevel: 1 }));

      renderPage();

      const approveButton = await screen.findByRole('button', { name: 'Approve' });
      expect(approveButton).toBeDisabled();
      expect(screen.getByText(/need an Administrator/)).toBeInTheDocument();
      expect(approveAgentWorkflow).not.toHaveBeenCalled();
    });

    it('lets an Administrator approve a high-impact proposal', async () => {
      const user = userEvent.setup();
      vi.mocked(useAuth).mockReturnValue({ token: 'token-1', user: { role: 'Administrator' } });
      vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow({ impactLevel: 1 }));
      vi.mocked(approveAgentWorkflow).mockResolvedValue(buildWorkflow({ impactLevel: 1, status: 4 }));

      renderPage();

      const approveButton = await screen.findByRole('button', { name: 'Approve' });
      expect(approveButton).toBeEnabled();

      await user.click(approveButton);

      await waitFor(() => expect(approveAgentWorkflow).toHaveBeenCalledWith('token-1', 'wf-1', ''));
    });

    it('treats a duplicate approval as a conflict and reloads the true state', async () => {
      const user = userEvent.setup();
      vi.mocked(getAgentWorkflow)
        .mockResolvedValueOnce(buildWorkflow())
        .mockResolvedValueOnce(
          buildWorkflow({
            status: 4,
            approvals: [{ status: 1, requestedAt: '2026-10-16T00:00:00Z', reviewedByUserId: 9, reviewedAt: '2026-10-16T00:05:00Z' }],
          })
        );
      vi.mocked(approveAgentWorkflow).mockRejectedValue({
        status: 409,
        message: 'Only workflows awaiting approval can be approved (current status: Completed).',
      });

      renderPage();
      await user.click(await screen.findByRole('button', { name: 'Approve' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Only workflows awaiting approval can be approved (current status: Completed).'
      );

      // The page reflects the real, current state: someone else already decided.
      await waitFor(() => expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument());
      expect(within(screen.getByTestId('workflow-status')).getByText('Completed')).toBeInTheDocument();
    });
  });

  describe('rejecting', () => {
    it('asks for confirmation before rejecting, then persists the comment', async () => {
      const user = userEvent.setup();
      vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow());
      vi.mocked(rejectAgentWorkflow).mockResolvedValue(buildWorkflow({ status: 6 }));

      renderPage();
      await user.type(await screen.findByLabelText('Comment (optional)'), 'Not this month.');

      await user.click(screen.getByRole('button', { name: 'Reject' }));
      expect(rejectAgentWorkflow).not.toHaveBeenCalled(); // still awaiting confirmation

      await user.click(screen.getByRole('button', { name: 'Yes, reject' }));

      await waitFor(() =>
        expect(rejectAgentWorkflow).toHaveBeenCalledWith('token-1', 'wf-1', 'Not this month.')
      );
      expect(await screen.findByText('Proposal rejected.')).toBeInTheDocument();
    });
  });

  describe('requesting a revision', () => {
    it('requires a comment before submitting', async () => {
      const user = userEvent.setup();
      vi.mocked(getAgentWorkflow).mockResolvedValue(buildWorkflow());

      renderPage();
      await user.click(await screen.findByRole('button', { name: 'Request revision' }));
      await user.click(screen.getByRole('button', { name: 'Submit revision request' }));

      expect(await screen.findByText(/Explain what should change/)).toBeInTheDocument();
      expect(reviseAgentWorkflow).not.toHaveBeenCalled();
    });

    it('sends the comment and constraints, then returns to the correct stage', async () => {
      const user = userEvent.setup();
      // The component always re-fetches from the server after an action
      // rather than trusting the action's own response body, so the second
      // GET (the post-revision reload) is what must carry the new proposal.
      vi.mocked(getAgentWorkflow)
        .mockResolvedValueOnce(buildWorkflow())
        .mockResolvedValueOnce(
          buildWorkflow({
            status: 3,
            approvals: [
              { status: 3, requestedAt: '2026-10-16T00:00:10Z', reviewedByUserId: 5, reviewedAt: '2026-10-16T00:10:00Z', comment: 'Keep discounts at 10% or less.' },
              { status: 0, requestedAt: '2026-10-16T00:10:01Z' },
            ],
          })
        );
      vi.mocked(reviseAgentWorkflow).mockResolvedValue({});

      renderPage();
      await user.click(await screen.findByRole('button', { name: 'Request revision' }));

      await user.type(screen.getByLabelText(/^What should change/), 'Keep discounts at 10% or less.');
      await user.clear(screen.getByLabelText('New maximum discount (%)'));
      await user.type(screen.getByLabelText('New maximum discount (%)'), '10');
      await user.click(screen.getByLabelText('Spaghetti Carbonara'));

      await user.click(screen.getByRole('button', { name: 'Submit revision request' }));

      await waitFor(() =>
        expect(reviseAgentWorkflow).toHaveBeenCalledWith('token-1', 'wf-1', {
          comment: 'Keep discounts at 10% or less.',
          maxDiscountPercent: 10,
          maxProposals: null,
          excludeProductIds: ['prod-1'],
        })
      );
      expect(await screen.findByText('Revision requested. A new proposal has been drafted.')).toBeInTheDocument();
      // The workflow returns to AwaitingApproval for the new proposal.
      expect(screen.getByText('Awaiting approval')).toBeInTheDocument();
      expect(screen.getByText('Revision requested')).toBeInTheDocument(); // audit history
    });
  });
});
