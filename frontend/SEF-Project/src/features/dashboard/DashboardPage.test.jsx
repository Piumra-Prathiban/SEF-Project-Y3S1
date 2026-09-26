import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import DashboardPage from './DashboardPage';
import { getInventory, getProducts } from '../../services/catalogApi';
import { getOrders } from '../../services/orderService';

vi.mock('../../contexts/AuthContext', () => ({ useAuth: () => ({ token: 'staff-token', user: { email: 'staff@example.com', role: 'Staff' } }) }));
vi.mock('../../hooks/useSessionGuard', () => { const guard = () => false; return { useSessionGuard: () => guard }; });
vi.mock('../../services/catalogApi', () => ({ getInventory: vi.fn(), getProducts: vi.fn() }));
vi.mock('../../services/orderService', async (importOriginal) => ({ ...await importOriginal(), getOrders: vi.fn() }));

beforeEach(() => {
  vi.clearAllMocks();
  getProducts.mockResolvedValue({ items: [], totalItems: 12 });
  getInventory.mockResolvedValue([{ id: 'stock-1', isLowStock: true }, { id: 'stock-2', isLowStock: false }]);
  getOrders.mockResolvedValue({ totalCount: 3, items: [{ id: 'order-1', orderNumber: 'ORD-1001', status: 0, placedAt: '2026-09-26T09:00:00Z', total: 3500, currency: 'LKR' }] });
});

it('shows live catalogue, stock and order figures with a recent order link', async () => {
  render(<MemoryRouter><DashboardPage /></MemoryRouter>);
  const metrics = (await screen.findByText('12')).closest('a');
  expect(within(metrics).getByText('Products')).toBeInTheDocument();
  expect(screen.getByText('Low stock').closest('a')).toHaveAttribute('href', '/inventory/low-stock');
  expect(screen.getByText('ORD-1001').closest('a')).toHaveAttribute('href', '/orders/order-1');
  expect(screen.getByText(/3,500\.00/)).toBeInTheDocument();
});
