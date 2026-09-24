import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import OrdersListPage from './OrdersListPage';
import OrderDetailPage from './OrderDetailPage';
import { getOrderById, getOrders } from '../../services/orderService';

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    getOrders: vi.fn(),
    getOrderById: vi.fn(),
  };
});

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const ORDER_A = {
  id: '11111111-1111-1111-1111-111111111111',
  orderNumber: 'ORD-1001',
  status: 0,
  placedAt: '2026-09-20T10:00:00Z',
  total: 2500.5,
  currency: 'LKR',
};

const ORDER_B = {
  id: '22222222-2222-2222-2222-222222222222',
  orderNumber: 'ORD-1002',
  status: 4,
  placedAt: '2026-09-21T11:00:00Z',
  total: 1800,
  currency: 'LKR',
};

const ORDER_C = {
  id: '33333333-3333-3333-3333-333333333333',
  orderNumber: 'ORD-2001',
  status: 1,
  placedAt: '2026-09-22T12:00:00Z',
  total: 950,
  currency: 'LKR',
};

const ORDER_DETAIL = {
  id: ORDER_A.id,
  orderNumber: ORDER_A.orderNumber,
  status: ORDER_A.status,
  placedAt: ORDER_A.placedAt,
  subtotal: 2300,
  discountTotal: 0,
  taxAmount: 0,
  shippingFee: 200.5,
  total: ORDER_A.total,
  currency: ORDER_A.currency,
  items: [
    {
      id: '99999999-9999-9999-9999-999999999999',
      productVariantId: '88888888-8888-8888-8888-888888888888',
      sku: 'DEN-001',
      name: 'Slim Fit Denim Jacket',
      quantity: 2,
      unitPrice: 1150,
      lineTotal: 2300,
    },
  ],
  deliveryAddress: {
    fullName: 'Asha Perera',
    line1: '12 Galle Road',
    line2: null,
    city: 'Colombo',
    province: null,
    postalCode: '00300',
    country: 'Sri Lanka',
    phone: null,
  },
  payments: [],
  shipments: [],
  statusHistory: [],
};

function buildListResponse({ items, totalCount, page = 1, pageSize = 20 } = {}) {
  return {
    items: items ?? [ORDER_A, ORDER_B],
    totalCount: totalCount ?? items?.length ?? 2,
    page,
    pageSize,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/orders']}>
      <Routes>
        <Route path="/orders" element={<OrdersListPage />} />
        <Route path="/orders/:id" element={<OrderDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();

  mockAuth = {
    user: { id: 1, email: 'customer@example.com', role: 'Customer' },
    token: 'test-token',
    expiresAt: null,
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    fetchCurrentUser: vi.fn(),
  };

  getOrders.mockResolvedValue(buildListResponse());
  getOrderById.mockResolvedValue(ORDER_DETAIL);
});

describe('OrdersListPage', () => {
  it('renders orders on successful load', async () => {
    renderPage();

    expect(await screen.findByText('ORD-1001')).toBeInTheDocument();
    expect(screen.getByText('ORD-1002')).toBeInTheDocument();

    const table = screen.getByRole('table');
    expect(within(table).getByText('Pending')).toBeInTheDocument();
    expect(within(table).getByText('Completed')).toBeInTheDocument();
    expect(within(table).getAllByText(/LKR/)).toHaveLength(2);
  });

  it('shows a loading state while the request is in flight', () => {
    getOrders.mockImplementation(() => new Promise(() => {}));

    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Loading');
  });

  it('shows an empty state when there are no orders', async () => {
    getOrders.mockResolvedValue(
      buildListResponse({ items: [], totalCount: 0 }),
    );

    renderPage();

    expect(await screen.findByText('No orders found.')).toBeInTheDocument();
  });

  it('shows an error and recovers via retry', async () => {
    getOrders.mockRejectedValueOnce(
      Object.assign(new Error('API down'), { status: 500, data: null }),
    );

    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('API down');

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('ORD-1001')).toBeInTheDocument();
    expect(getOrders).toHaveBeenCalledTimes(2);
  });

  it('paginates with previous/next controls', async () => {
    getOrders
      .mockResolvedValueOnce(
        buildListResponse({ items: [ORDER_A, ORDER_B], totalCount: 45 }),
      )
      .mockResolvedValueOnce(
        buildListResponse({ items: [ORDER_C], totalCount: 45, page: 2 }),
      );

    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText('Page 1 of 3 (45 orders)')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Next' }));

    expect(await screen.findByText('ORD-2001')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 3 (45 orders)')).toBeInTheDocument();
    expect(getOrders).toHaveBeenLastCalledWith(
      'test-token',
      expect.objectContaining({ page: 2 }),
    );
  });

  it('applies status, sorting, and date filters', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('ORD-1001');

    await user.selectOptions(screen.getByLabelText('Status'), 'Pending');
    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ status: 'Pending' }),
      );
    });

    await user.selectOptions(screen.getByLabelText('Sort by'), 'total');
    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ sortBy: 'total' }),
      );
    });

    await user.selectOptions(screen.getByLabelText('Sort direction'), 'asc');
    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ sortDirection: 'asc' }),
      );
    });

    const expectedFrom = new Date(2026, 8, 1).toISOString();
    fireEvent.change(screen.getByLabelText('From'), {
      target: { value: '2026-09-01' },
    });
    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ from: expectedFrom }),
      );
    });

    const expectedTo = new Date(2026, 8, 30, 23, 59, 59, 999).toISOString();
    fireEvent.change(screen.getByLabelText('To'), {
      target: { value: '2026-09-30' },
    });
    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ to: expectedTo }),
      );
    });
  });

  it('searches by order number', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('ORD-1001');

    await user.type(screen.getByLabelText('Order number'), 'ORD-42');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    await waitFor(() => {
      expect(getOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ orderNumber: 'ORD-42' }),
      );
    });
  });

  it.each(['Staff', 'Administrator'])(
    'shows the customer filter to %s users and sends it',
    async (role) => {
      mockAuth.user = { ...mockAuth.user, role };

      const user = userEvent.setup();
      renderPage();

      await screen.findByText('ORD-1001');

      await user.type(screen.getByLabelText('Customer ID'), '42');
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      await waitFor(() => {
        expect(getOrders).toHaveBeenLastCalledWith(
          'test-token',
          expect.objectContaining({ customerId: 42 }),
        );
      });
    },
  );

  it('hides the customer filter from customers', async () => {
    renderPage();

    await screen.findByText('ORD-1001');

    expect(screen.queryByLabelText('Customer ID')).not.toBeInTheDocument();
  });

  it('opens order details when an order is clicked', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('link', { name: 'ORD-1001' }));

    expect(
      await screen.findByRole('heading', { name: 'Order ORD-1001' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Slim Fit Denim Jacket')).toBeInTheDocument();
    expect(getOrderById).toHaveBeenCalledWith('test-token', ORDER_A.id);
  });
});
