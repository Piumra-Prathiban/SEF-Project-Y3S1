import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { InventoryAgentPage } from './InventoryAgentPage';

const api = vi.hoisted(() => ({
  getInventory: vi.fn(),
  listInventoryWorkflows: vi.fn(),
  getStockHistory: vi.fn(),
  createInventoryWorkflow: vi.fn(),
  getInventoryWorkflow: vi.fn(),
  approveInventoryWorkflow: vi.fn(),
  rejectInventoryWorkflow: vi.fn(),
  reviseInventoryWorkflow: vi.fn(),
}));

vi.mock('../../hooks/useCatalogApi', () => ({ useCatalogApi: () => api }));
vi.mock('../../contexts/AuthContext', () => ({ useAuth: () => ({ user: { role: 'Staff' } }) }));

beforeEach(() => {
  vi.clearAllMocks();
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  api.getInventory.mockResolvedValue([{ productVariantId: 'variant-1', productName: 'Cotton Shirt', sku: 'TSH-M' }]);
  api.listInventoryWorkflows.mockResolvedValue({ items: [], page: 1, totalPages: 1, totalItems: 0 });
  api.getStockHistory.mockResolvedValue([]);
  api.createInventoryWorkflow.mockResolvedValue({
    workflowId: 'workflow-1',
    objective: 'Check low stock',
    status: 'AwaitingApproval',
    approvals: [{ status: 'Pending' }],
    steps: [{ status: 'Completed', result: { recommendations: [{ variantId: 'variant-1', productName: 'Cotton Shirt', sku: 'TSH-M', currentStock: 2, reorderLevel: 5, recommendedAction: 'RESTOCK', recommendedQuantity: 10, reason: 'Low stock' }] }, toolExecutions: [], validationSummaries: ['Validated'] }],
  });
  api.approveInventoryWorkflow.mockResolvedValue({ workflowId: 'workflow-1', status: 'Completed', steps: [{ status: 'Completed', result: { recommendations: [{ variantId: 'variant-1' }] } }], approvals: [{ status: 'Approved' }] });
});

it('selects a named variant and reviews the real awaiting-approval workflow', async () => {
  const user = userEvent.setup();
  render(<MemoryRouter><InventoryAgentPage /></MemoryRouter>);

  await user.click(await screen.findByRole('checkbox', { name: /Cotton Shirt TSH-M/ }));
  await user.click(screen.getByRole('button', { name: 'Start workflow' }));

  await waitFor(() => expect(api.createInventoryWorkflow).toHaveBeenCalledWith(expect.objectContaining({ variantIds: ['variant-1'] })));
  expect(await screen.findByRole('heading', { name: 'Human approval review' })).toBeInTheDocument();
  expect(screen.getByText('Low stock')).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Approve' }));
  await waitFor(() => expect(api.approveInventoryWorkflow).toHaveBeenCalledWith('workflow-1', { comment: null }));
  expect(window.confirm).toHaveBeenCalledWith(expect.stringContaining('may add 10 units'));
  await waitFor(() => expect(api.getStockHistory).toHaveBeenCalledWith('variant-1'));
});

it('opens a workflow from the pending approval queue', async () => {
  const user = userEvent.setup();
  api.listInventoryWorkflows.mockResolvedValue({ items: [{ workflowId: 'workflow-2', objective: 'Review jackets', status: 'AwaitingApproval', startedAt: '2026-09-26T09:00:00Z' }], page: 1, totalPages: 1, totalItems: 1 });
  api.getInventoryWorkflow.mockResolvedValue({ workflowId: 'workflow-2', objective: 'Review jackets', status: 'AwaitingApproval', approvals: [{ status: 'Pending' }], steps: [] });
  render(<MemoryRouter><InventoryAgentPage /></MemoryRouter>);

  await user.click(await screen.findByRole('button', { name: 'Open workflow' }));
  await waitFor(() => expect(api.getInventoryWorkflow).toHaveBeenCalledWith('workflow-2'));
  expect(await screen.findByRole('heading', { name: 'Human approval review' })).toBeInTheDocument();
});
