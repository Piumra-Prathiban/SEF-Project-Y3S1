import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import OrderDetailPage from './OrderDetailPage';
import {
  getOrderById,
  updateOrderStatus,
  OrderStatus,
} from '../../services/orderService';

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    getOrderById: vi.fn(),
    updateOrderStatus: vi.fn(),
  };
});

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const ORDER_ID = '11111111-1111-1111-1111-111111111111';

const ORDER = {
  id: ORDER_ID,
  orderNumber: 'ORD-1001',
  status: 4,
  placedAt: '2026-09-20T10:00:00Z',
  subtotal: 2300,
  discountTotal: 200,
  taxAmount: 105,
  shippingFee: 300.5,
  total: 2505.5,
  currency: 'LKR',
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
    line2: 'Apartment 4B',
    city: 'Colombo',
    province: 'Western',
    postalCode: '00300',
    country: 'Sri Lanka',
    phone: '+94 77 123 4567',
  },
  payments: [
    {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      amount: 2505.5,
      method: 0,
      status: 1,
      transactionReference: 'MOCK-1234',
      paidAt: '2026-09-20T10:05:00Z',
    },
  ],
  shipments: [
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      status: 1,
      trackingNumber: 'TRACK-99',
      carrier: 'LankaExpress',
      shippedAt: '2026-09-21T08:00:00Z',
      deliveredAt: null,
    },
  ],
  statusHistory: [
    {
      id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      status: 0,
      changedAt: '2026-09-20T10:00:00Z',
      note: 'Order placed',
    },
    {
      id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
      status: 4,
      changedAt: '2026-09-22T18:30:00Z',
      note: null,
    },
  ],
};

function makeOrder(overrides) {
  return { ...ORDER, ...overrides };
}

const ORDER_PENDING = makeOrder({
  status: OrderStatus.Pending,
  payments: [],
  shipments: [],
  statusHistory: [
    {
      id: 'hhhhhhhh-hhhh-hhhh-hhhh-hhhhhhhhhhhh',
      status: OrderStatus.Pending,
      changedAt: '2026-09-20T10:00:00Z',
      note: 'Order placed',
    },
  ],
});

function renderPage() {
  return render(
    <MemoryRouter initialEntries={[`/orders/${ORDER_ID}`]}>
      <Routes>
        <Route path="/orders/:id" element={<OrderDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();

  mockAuth = {
    user: { id: 1, email: 'staff@example.com', role: 'Staff' },
    token: 'test-token',
    expiresAt: null,
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    fetchCurrentUser: vi.fn(),
  };

  vi.spyOn(window, 'confirm').mockReturnValue(true);

  getOrderById.mockResolvedValue(ORDER);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('OrderDetailPage', () => {
  it('renders the order details, items, totals, payments and shipments', async () => {
    renderPage();

    expect(
      await screen.findByRole('heading', { name: 'Order ORD-1001' }),
    ).toBeInTheDocument();

    expect(screen.getByText('Slim Fit Denim Jacket')).toBeInTheDocument();
    expect(screen.getByText('DEN-001')).toBeInTheDocument();

    expect(screen.getByText('Subtotal')).toBeInTheDocument();
    expect(screen.getByText('Discount')).toBeInTheDocument();
    expect(screen.getByText('Tax')).toBeInTheDocument();
    expect(screen.getByText('Shipping')).toBeInTheDocument();

    expect(screen.getByText('Asha Perera')).toBeInTheDocument();
    expect(screen.getByText(/12 Galle Road/)).toBeInTheDocument();

    expect(screen.getByText('Card')).toBeInTheDocument();
    expect(screen.getByText('MOCK-1234')).toBeInTheDocument();

    expect(screen.getByText('LankaExpress')).toBeInTheDocument();
    expect(screen.getByText('TRACK-99')).toBeInTheDocument();
  });

  it('renders the status timeline chronologically', async () => {
    renderPage();

    const timeline = await screen.findByRole('list', {
      name: 'Status history timeline',
    });

    const entries = within(timeline).getAllByRole('listitem');
    expect(entries).toHaveLength(2);
    expect(within(entries[0]).getByText('Pending')).toBeInTheDocument();
    expect(within(entries[0]).getByText('Order placed')).toBeInTheDocument();
    expect(within(entries[1]).getByText('Completed')).toBeInTheDocument();
  });

  it('shows a not-found state when the order does not exist', async () => {
    getOrderById.mockRejectedValue(
      Object.assign(new Error('Order not found.'), { status: 404, data: null }),
    );

    renderPage();

    expect(
      await screen.findByRole('heading', { name: 'Order not found' }),
    ).toBeInTheDocument();
  });

  it('shows an error and recovers via retry when the request fails', async () => {
    getOrderById.mockRejectedValueOnce(
      Object.assign(new Error('API down'), { status: 500, data: null }),
    );

    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('API down');

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(
      await screen.findByRole('heading', { name: 'Order ORD-1001' }),
    ).toBeInTheDocument();
  });

  it('offers staff only the next allowed order statuses', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    const options = within(screen.getByLabelText('New status'))
      .getAllByRole('option')
      .map((option) => option.textContent);

    expect(options).toEqual(['Select a status…', 'Confirmed']);
  });

  it('hides the status controls from customers', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };
    getOrderById.mockResolvedValue(ORDER_PENDING);

    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    expect(screen.queryByLabelText('New status')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Update status' }),
    ).not.toBeInTheDocument();
  });

  it('updates the order status for staff', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    updateOrderStatus.mockResolvedValue(
      makeOrder({ status: OrderStatus.Confirmed }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New status'),
      String(OrderStatus.Confirmed),
    );
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    await waitFor(() => {
      expect(updateOrderStatus).toHaveBeenCalledWith('test-token', ORDER_ID, {
        status: OrderStatus.Confirmed,
      });
    });

    expect(
      await screen.findByText('Order status updated to Confirmed.'),
    ).toBeInTheDocument();
  });

  it('refreshes the order and status history after a successful update', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    updateOrderStatus.mockResolvedValue(
      makeOrder({
        status: OrderStatus.Confirmed,
        statusHistory: [
          ...ORDER_PENDING.statusHistory,
          {
            id: 'iiiiiiii-iiii-iiii-iiii-iiiiiiiiiiii',
            status: OrderStatus.Confirmed,
            changedAt: '2026-09-24T09:00:00Z',
            note: 'Confirmed by staff',
          },
        ],
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    expect(
      within(
        screen.getByRole('list', { name: 'Status history timeline' }),
      ).getAllByRole('listitem'),
    ).toHaveLength(1);

    await user.selectOptions(
      screen.getByLabelText('New status'),
      String(OrderStatus.Confirmed),
    );
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    await waitFor(() => {
      expect(
        within(
          screen.getByRole('list', { name: 'Status history timeline' }),
        ).getAllByRole('listitem'),
      ).toHaveLength(2);
    });

    const timeline = screen.getByRole('list', {
      name: 'Status history timeline',
    });

    expect(within(timeline).getByText('Confirmed')).toBeInTheDocument();
    expect(within(timeline).getByText('Confirmed by staff')).toBeInTheDocument();
  });

  it('surfaces a permission error when the backend rejects the change', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    updateOrderStatus.mockRejectedValue(
      Object.assign(new Error('Only staff can change order status.'), {
        status: 403,
        data: null,
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New status'),
      String(OrderStatus.Confirmed),
    );
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    expect(
      await screen.findByText(
        'You do not have permission to change the order status.',
      ),
    ).toBeInTheDocument();
  });

  it('keeps the current status when a transition is rejected', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    updateOrderStatus.mockRejectedValue(
      Object.assign(
        new Error("Transition from 'Pending' to 'Confirmed' is not allowed."),
        { status: 409, data: null },
      ),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New status'),
      String(OrderStatus.Confirmed),
    );
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    expect(
      await screen.findByText(
        'That status change is not allowed from the current status.',
      ),
    ).toBeInTheDocument();

    expect(
      within(
        screen.getByRole('list', { name: 'Status history timeline' }),
      ).getAllByRole('listitem'),
    ).toHaveLength(1);
    expect(screen.queryByText(/Order status updated/)).not.toBeInTheDocument();
  });

  it('asks for confirmation before applying a terminal status', async () => {
    getOrderById.mockResolvedValue(
      makeOrder({
        status: OrderStatus.Ready,
        statusHistory: [
          {
            id: 'kkkkkkkk-kkkk-kkkk-kkkk-kkkkkkkkkkkk',
            status: OrderStatus.Ready,
            changedAt: '2026-09-24T08:00:00Z',
            note: null,
          },
        ],
      }),
    );

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New status'),
      String(OrderStatus.Completed),
    );
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    expect(confirmSpy).toHaveBeenCalled();
    expect(updateOrderStatus).not.toHaveBeenCalled();
  });
});
