import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import OrderDetailPage from './OrderDetailPage';
import { getOrderById } from '../../services/orderService';

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    getOrderById: vi.fn(),
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

  getOrderById.mockResolvedValue(ORDER);
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
});
