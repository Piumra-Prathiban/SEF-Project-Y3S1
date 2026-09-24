import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import OrderDetailPage from './OrderDetailPage';
import {
  getOrderById,
  updateOrderStatus,
  createPayment,
  updatePaymentStatus,
  createShipment,
  updateShipmentStatus,
  cancelOrder,
  OrderStatus,
  PaymentMethod,
  PaymentStatus,
  ShipmentStatus,
} from '../../services/orderService';

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    getOrderById: vi.fn(),
    updateOrderStatus: vi.fn(),
    createPayment: vi.fn(),
    updatePaymentStatus: vi.fn(),
    createShipment: vi.fn(),
    updateShipmentStatus: vi.fn(),
    cancelOrder: vi.fn(),
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

const PAYMENT_PENDING = {
  id: 'llllllll-llll-llll-llll-llllllllllll',
  amount: 500,
  method: PaymentMethod.OnlineTransfer,
  status: PaymentStatus.Pending,
  transactionReference: null,
  paidAt: null,
};

const PAYMENT_COMPLETED_UPDATED = {
  ...PAYMENT_PENDING,
  status: PaymentStatus.Completed,
  transactionReference: 'MOCK-99999999',
  paidAt: '2026-09-24T09:30:00Z',
};

const ORDER_WITH_PENDING_PAYMENT = {
  ...ORDER_PENDING,
  payments: [PAYMENT_PENDING],
};

const SHIPMENT_PENDING = {
  id: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
  status: ShipmentStatus.Pending,
  trackingNumber: null,
  carrier: null,
  shippedAt: null,
  deliveredAt: null,
};

const SHIPMENT_SHIPPED = {
  ...SHIPMENT_PENDING,
  status: ShipmentStatus.Shipped,
  carrier: 'DHL Express',
  trackingNumber: 'DHL-123',
  shippedAt: '2026-09-24T10:00:00Z',
};

const ORDER_WITH_PENDING_SHIPMENT = {
  ...ORDER_PENDING,
  shipments: [SHIPMENT_PENDING],
};

const CANCELLED_ORDER = makeOrder({
  status: OrderStatus.Cancelled,
  payments: [
    {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      amount: 2505.5,
      method: PaymentMethod.Card,
      status: PaymentStatus.Refunded,
      transactionReference: 'MOCK-1234',
      paidAt: '2026-09-20T10:05:00Z',
    },
  ],
  shipments: [
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      status: ShipmentStatus.Cancelled,
      trackingNumber: 'TRACK-99',
      carrier: 'LankaExpress',
      shippedAt: null,
      deliveredAt: null,
    },
  ],
});

function paymentsTable() {
  return within(
    screen.getByRole('region', { name: 'Payments' }),
  ).getByRole('table');
}

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

    const table = paymentsTable();
    expect(within(table).getByText('Card')).toBeInTheDocument();
    expect(within(table).getByText('MOCK-1234')).toBeInTheDocument();

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

  it('shows payment records and a payment action to customers without staff controls', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };

    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    const table = paymentsTable();

    expect(within(table).getByText('Card')).toBeInTheDocument();
    expect(within(table).getByText('MOCK-1234')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Submit payment' }),
    ).toBeInTheDocument();
    expect(
      screen.queryByLabelText(/Update .* payment/),
    ).not.toBeInTheDocument();
  });

  it('offers staff only the next allowed payment statuses', async () => {
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    const options = within(screen.getByLabelText('Update Card payment'))
      .getAllByRole('option')
      .map((option) => option.textContent);

    expect(options).toEqual(['Select…', 'Refunded']);
  });

  it('lets staff complete a pending payment and refreshes the order', async () => {
    getOrderById
      .mockResolvedValueOnce(ORDER_WITH_PENDING_PAYMENT)
      .mockResolvedValueOnce({
        ...ORDER_WITH_PENDING_PAYMENT,
        payments: [PAYMENT_COMPLETED_UPDATED],
      });

    updatePaymentStatus.mockResolvedValue(PAYMENT_COMPLETED_UPDATED);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('Update OnlineTransfer payment'),
      String(PaymentStatus.Completed),
    );
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    await waitFor(() => {
      expect(updatePaymentStatus).toHaveBeenCalledWith(
        'test-token',
        ORDER_ID,
        PAYMENT_PENDING.id,
        { status: PaymentStatus.Completed },
      );
    });

    expect(
      await screen.findByText('Payment marked as Completed.'),
    ).toBeInTheDocument();

    expect(within(paymentsTable()).getByText('MOCK-99999999')).toBeInTheDocument();
    expect(getOrderById).toHaveBeenCalledTimes(2);
  });

  it('surfaces a permission error when the backend rejects a payment update', async () => {
    getOrderById.mockResolvedValue(ORDER_WITH_PENDING_PAYMENT);
    updatePaymentStatus.mockRejectedValue(
      Object.assign(new Error('Only staff can update payments.'), {
        status: 403,
        data: null,
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('Update OnlineTransfer payment'),
      String(PaymentStatus.Completed),
    );
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(
      await screen.findByText(
        'You do not have permission to perform this payment action.',
      ),
    ).toBeInTheDocument();
  });

  it('shows the server message when a payment status change conflicts', async () => {
    getOrderById.mockResolvedValue(ORDER_WITH_PENDING_PAYMENT);
    updatePaymentStatus.mockRejectedValue(
      Object.assign(
        new Error("Transition from 'Pending' to 'Completed' is not allowed."),
        { status: 409, data: null },
      ),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('Update OnlineTransfer payment'),
      String(PaymentStatus.Completed),
    );
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(
      await screen.findByText(
        "Transition from 'Pending' to 'Completed' is not allowed.",
      ),
    ).toBeInTheDocument();
  });

  it('records a payment and shows it as pending', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };

    getOrderById
      .mockResolvedValueOnce(makeOrder({ payments: [] }))
      .mockResolvedValueOnce(ORDER_WITH_PENDING_PAYMENT);

    createPayment.mockResolvedValue(PAYMENT_PENDING);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });
    expect(screen.getByText('No payments recorded.')).toBeInTheDocument();

    await user.selectOptions(
      screen.getByLabelText('Method'),
      String(PaymentMethod.OnlineTransfer),
    );
    await user.type(screen.getByLabelText(/Amount/), '500');
    await user.click(screen.getByRole('button', { name: 'Submit payment' }));

    await waitFor(() => {
      expect(createPayment).toHaveBeenCalledWith('test-token', ORDER_ID, {
        method: PaymentMethod.OnlineTransfer,
        amount: 500,
      });
    });

    expect(
      await screen.findByText(
        'Payment recorded with status Pending. Staff confirm it once processed.',
      ),
    ).toBeInTheDocument();

    expect(within(paymentsTable()).getByText('Pending')).toBeInTheDocument();
  });

  it('shows a conflict message when a payment amount is rejected', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };

    getOrderById.mockResolvedValue(makeOrder({ payments: [] }));
    createPayment.mockRejectedValue(
      Object.assign(
        new Error('Payment amount exceeds the outstanding balance.'),
        { status: 409, data: null },
      ),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('Method'),
      String(PaymentMethod.Card),
    );
    await user.type(screen.getByLabelText(/Amount/), '999999');
    await user.click(screen.getByRole('button', { name: 'Submit payment' }));

    expect(
      await screen.findByText(
        'Payment amount exceeds the outstanding balance.',
      ),
    ).toBeInTheDocument();
  });

  it('shows shipment progress and tracking to customers without staff controls', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };

    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    const shipments = screen.getByRole('region', { name: 'Shipments' });

    expect(within(shipments).getByText('LankaExpress')).toBeInTheDocument();
    expect(within(shipments).getByText('TRACK-99')).toBeInTheDocument();
    expect(within(shipments).getByText('Shipped at')).toBeInTheDocument();
    expect(within(shipments).getByText('Delivered at')).toBeInTheDocument();

    const progress = within(shipments).getByRole('list', {
      name: 'Shipment progress',
    });

    expect(within(progress).getAllByRole('listitem')).toHaveLength(3);
    expect(within(progress).getByText('Shipped')).toHaveAttribute(
      'aria-current',
      'step',
    );

    expect(
      within(shipments).queryByLabelText('New shipment status'),
    ).not.toBeInTheDocument();
    expect(
      within(shipments).queryByRole('button', { name: 'Create shipment' }),
    ).not.toBeInTheDocument();
  });

  it('hides shipment controls from customers when there is no shipment', async () => {
    mockAuth.user = { ...mockAuth.user, role: 'Customer' };
    getOrderById.mockResolvedValue(ORDER_PENDING);

    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    expect(screen.getByText('No shipments recorded.')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Create shipment' }),
    ).not.toBeInTheDocument();
  });

  it('lets staff create a shipment and refreshes the order', async () => {
    getOrderById
      .mockResolvedValueOnce(ORDER_PENDING)
      .mockResolvedValueOnce(ORDER_WITH_PENDING_SHIPMENT);

    createShipment.mockResolvedValue(SHIPMENT_PENDING);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.type(screen.getByLabelText('Carrier'), 'DHL Express');
    await user.type(screen.getByLabelText('Tracking number'), 'DHL-123');
    await user.click(screen.getByRole('button', { name: 'Create shipment' }));

    await waitFor(() => {
      expect(createShipment).toHaveBeenCalledWith('test-token', ORDER_ID, {
        carrier: 'DHL Express',
        trackingNumber: 'DHL-123',
      });
    });

    expect(await screen.findByText('Shipment created.')).toBeInTheDocument();

    const shipments = screen.getByRole('region', { name: 'Shipments' });
    expect(
      within(shipments).getByRole('list', { name: 'Shipment progress' }),
    ).toBeInTheDocument();
    expect(getOrderById).toHaveBeenCalledTimes(2);
  });

  it('lets staff update a shipment status and tracking details', async () => {
    getOrderById
      .mockResolvedValueOnce(ORDER_WITH_PENDING_SHIPMENT)
      .mockResolvedValueOnce({
        ...ORDER_WITH_PENDING_SHIPMENT,
        shipments: [SHIPMENT_SHIPPED],
      });

    updateShipmentStatus.mockResolvedValue(SHIPMENT_SHIPPED);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New shipment status'),
      String(ShipmentStatus.Shipped),
    );
    await user.type(screen.getByLabelText('Carrier'), 'DHL Express');
    await user.click(screen.getByRole('button', { name: 'Update shipment' }));

    await waitFor(() => {
      expect(updateShipmentStatus).toHaveBeenCalledWith(
        'test-token',
        ORDER_ID,
        SHIPMENT_PENDING.id,
        {
          status: ShipmentStatus.Shipped,
          carrier: 'DHL Express',
          trackingNumber: null,
        },
      );
    });

    expect(
      await screen.findByText('Shipment marked as Shipped.'),
    ).toBeInTheDocument();
    expect(getOrderById).toHaveBeenCalledTimes(2);
  });

  it('surfaces a permission error when the backend rejects a shipment update', async () => {
    getOrderById.mockResolvedValue(ORDER_WITH_PENDING_SHIPMENT);
    updateShipmentStatus.mockRejectedValue(
      Object.assign(new Error('Only staff can manage shipments.'), {
        status: 403,
        data: null,
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.selectOptions(
      screen.getByLabelText('New shipment status'),
      String(ShipmentStatus.Shipped),
    );
    await user.click(screen.getByRole('button', { name: 'Update shipment' }));

    expect(
      await screen.findByText(
        'You do not have permission to manage shipments.',
      ),
    ).toBeInTheDocument();
  });

  it('shows the server message when shipment creation conflicts', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    createShipment.mockRejectedValue(
      Object.assign(new Error('A shipment already exists for this order.'), {
        status: 409,
        data: null,
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.click(screen.getByRole('button', { name: 'Create shipment' }));

    expect(
      await screen.findByText('A shipment already exists for this order.'),
    ).toBeInTheDocument();
  });

  it.each(['Customer', 'Staff'])(
    'lets %s users cancel an eligible order and refreshes everything',
    async (role) => {
      mockAuth.user = { ...mockAuth.user, role };
      getOrderById.mockResolvedValue(ORDER_PENDING);
      cancelOrder.mockResolvedValue(CANCELLED_ORDER);

      const user = userEvent.setup();
      renderPage();

      await screen.findByRole('heading', { name: 'Order ORD-1001' });

      await user.click(screen.getByRole('button', { name: 'Cancel order' }));

      await waitFor(() => {
        expect(cancelOrder).toHaveBeenCalledWith('test-token', ORDER_ID);
      });

      expect(await screen.findByText('Order cancelled.')).toBeInTheDocument();
      expect(
        screen.getByText('This order can no longer be cancelled.'),
      ).toBeInTheDocument();

      expect(within(paymentsTable()).getByText('Refunded')).toBeInTheDocument();

      const shipments = screen.getByRole('region', { name: 'Shipments' });
      expect(within(shipments).getByText('Cancelled')).toBeInTheDocument();
    },
  );

  it('requires confirmation before cancelling', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.click(screen.getByRole('button', { name: 'Cancel order' }));

    expect(window.confirm).toHaveBeenCalled();
    expect(cancelOrder).not.toHaveBeenCalled();
  });

  it('hides the cancellation action once the order can no longer be cancelled', async () => {
    getOrderById.mockResolvedValue(CANCELLED_ORDER);

    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    expect(
      screen.getByText('This order can no longer be cancelled.'),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Cancel order' }),
    ).not.toBeInTheDocument();
  });

  it('surfaces the backend conflict when cancellation is blocked', async () => {
    getOrderById.mockResolvedValue(ORDER_PENDING);
    cancelOrder.mockRejectedValue(
      Object.assign(
        new Error(
          'Order cannot be cancelled because it has already been shipped.',
        ),
        { status: 409, data: null },
      ),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByRole('heading', { name: 'Order ORD-1001' });

    await user.click(screen.getByRole('button', { name: 'Cancel order' }));

    expect(
      await screen.findByText(
        'Order cannot be cancelled because it has already been shipped.',
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Cancel order' }),
    ).toBeInTheDocument();
  });
});
