import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import PurchaseOrdersPage from './PurchaseOrdersPage';
import {
  cancelPurchaseOrder,
  createPurchaseOrder,
  getPurchaseOrders,
  getSuppliers,
  getVariantOptions,
  receivePurchaseOrder,
  submitPurchaseOrder,
} from './purchaseOrderService';

vi.mock('./purchaseOrderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    getPurchaseOrders: vi.fn(),
    getPurchaseOrderById: vi.fn(),
    createPurchaseOrder: vi.fn(),
    submitPurchaseOrder: vi.fn(),
    receivePurchaseOrder: vi.fn(),
    cancelPurchaseOrder: vi.fn(),
    getSuppliers: vi.fn(),
    getVariantOptions: vi.fn(),
  };
});

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const DRAFT = {
  id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  orderNumber: 'PO-2026-0001',
  supplierId: 'supplier-1',
  supplierName: 'Atlas Textiles',
  status: 0,
  expectedAt: '2026-03-01T00:00:00Z',
  submittedAt: null,
  receivedAt: null,
  notes: null,
  items: [
    {
      id: 'item-1',
      productVariantId: 'variant-1',
      sku: 'TSH-CLS-XS',
      productName: 'Classic Cotton T-Shirt',
      variantName: 'XS / Black',
      quantity: 5,
      unitCost: 30,
      lineTotal: 150,
    },
  ],
  total: 150,
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z',
};

const SUBMITTED = {
  ...DRAFT,
  id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  orderNumber: 'PO-2026-0002',
  status: 1,
  submittedAt: '2026-02-02T00:00:00Z',
  total: 200,
};

const RECEIVED = {
  ...DRAFT,
  id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  orderNumber: 'PO-2026-0003',
  status: 2,
  submittedAt: '2026-02-03T00:00:00Z',
  receivedAt: '2026-02-05T00:00:00Z',
  total: 90,
};

function buildListResponse({ items, page = 1, pageSize = 10, totalItems } = {}) {
  const list = items ?? [DRAFT, SUBMITTED, RECEIVED];
  const total = totalItems ?? list.length;

  return {
    items: list,
    page,
    pageSize,
    totalItems: total,
    totalPages: Math.max(1, Math.ceil(total / pageSize)),
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <PurchaseOrdersPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();

  mockAuth = {
    user: { id: 2, email: 'staff@example.com', role: 'Staff' },
    token: 'test-token',
    expiresAt: null,
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    fetchCurrentUser: vi.fn(),
  };

  vi.spyOn(window, 'confirm').mockReturnValue(true);

  getPurchaseOrders.mockResolvedValue(buildListResponse());
  getSuppliers.mockResolvedValue([
    { id: 'supplier-1', name: 'Atlas Textiles', isActive: true },
    { id: 'supplier-2', name: 'Nordic Footwear', isActive: true },
  ]);
  getVariantOptions.mockResolvedValue([
    {
      id: 'variant-1',
      sku: 'TSH-CLS-XS',
      productName: 'Classic Cotton T-Shirt',
      variantName: 'XS / Black',
    },
  ]);
});

describe('PurchaseOrdersPage', () => {
  it('renders purchase orders with status and totals', async () => {
    renderPage();

    expect(await screen.findByText('PO-2026-0001')).toBeInTheDocument();
    expect(screen.getByText('PO-2026-0002')).toBeInTheDocument();
    expect(screen.getByText('PO-2026-0003')).toBeInTheDocument();

    const table = screen.getByRole('table', { name: 'Purchase orders' });
    expect(within(table).getAllByText('Atlas Textiles')).toHaveLength(3);
    expect(within(table).getByText('Draft')).toBeInTheDocument();
    expect(within(table).getByText('Submitted')).toBeInTheDocument();
    expect(within(table).getByText('Received')).toBeInTheDocument();
    expect(within(table).getByText(/150/)).toBeInTheDocument();
    expect(screen.getByText(/Page 1 of 1 \(3 purchase orders\)/)).toBeInTheDocument();
  });

  it('shows only the actions allowed for each status', async () => {
    renderPage();

    await screen.findByText('PO-2026-0001');

    expect(screen.getByRole('button', { name: 'Submit PO-2026-0001' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel PO-2026-0001' })).toBeInTheDocument();

    expect(screen.getByRole('button', { name: 'Receive PO-2026-0002' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit PO-2026-0002' })).not.toBeInTheDocument();

    expect(screen.queryByRole('button', { name: /PO-2026-0003/ })).not.toBeInTheDocument();
  });

  it('shows an empty state when there are no purchase orders', async () => {
    getPurchaseOrders.mockResolvedValue(buildListResponse({ items: [] }));

    renderPage();

    expect(await screen.findByText('No purchase orders found.')).toBeInTheDocument();
  });

  it('shows an error and recovers via retry', async () => {
    getPurchaseOrders.mockRejectedValueOnce(
      Object.assign(new Error('API down'), { status: 500, data: null }),
    );

    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('API down');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('PO-2026-0001')).toBeInTheDocument();
    expect(getPurchaseOrders).toHaveBeenCalledTimes(2);
  });

  it('passes supplier and status filters to the service', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('PO-2026-0001');

    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Submitted');
    await waitFor(() => {
      expect(getPurchaseOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ status: 'Submitted', page: 1 }),
      );
    });

    await user.selectOptions(screen.getByLabelText('Filter by supplier'), 'supplier-1');
    await waitFor(() => {
      expect(getPurchaseOrders).toHaveBeenLastCalledWith(
        'test-token',
        expect.objectContaining({ supplierId: 'supplier-1' }),
      );
    });
  });

  it('creates a draft with the expected payload', async () => {
    createPurchaseOrder.mockResolvedValue({ ...DRAFT, orderNumber: 'PO-2026-0009' });

    const user = userEvent.setup();
    renderPage();

    await screen.findByText('PO-2026-0001');

    await user.click(screen.getByRole('button', { name: 'Create purchase order' }));

    await user.selectOptions(screen.getByLabelText('Supplier'), 'supplier-1');
    await user.selectOptions(screen.getByLabelText('Product variant'), 'variant-1');
    await user.clear(screen.getByLabelText('Quantity'));
    await user.type(screen.getByLabelText('Quantity'), '2');
    await user.type(screen.getByLabelText('Unit cost'), '10.5');
    await user.type(screen.getByLabelText('Notes'), 'Spring restock');

    await user.click(screen.getByRole('button', { name: 'Create draft' }));

    await waitFor(() => {
      expect(createPurchaseOrder).toHaveBeenCalledWith('test-token', {
        supplierId: 'supplier-1',
        expectedAt: null,
        notes: 'Spring restock',
        items: [{ productVariantId: 'variant-1', quantity: 2, unitCost: 10.5 }],
      });
    });

    expect(await screen.findByText(/PO-2026-0009 created/)).toBeInTheDocument();
  });

  it('shows a validation error instead of calling the service', async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText('PO-2026-0001');

    await user.click(screen.getByRole('button', { name: 'Create purchase order' }));
    await user.click(screen.getByRole('button', { name: 'Create draft' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Select a supplier for this purchase order.',
    );
    expect(createPurchaseOrder).not.toHaveBeenCalled();
  });

  it('submits a draft purchase order after confirmation', async () => {
    submitPurchaseOrder.mockResolvedValue({ ...DRAFT, status: 1 });

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Submit PO-2026-0001' }),
    );

    expect(window.confirm).toHaveBeenCalled();
    await waitFor(() => {
      expect(submitPurchaseOrder).toHaveBeenCalledWith('test-token', DRAFT.id);
    });
    expect(await screen.findByText('Purchase order PO-2026-0001 submitted.')).toBeInTheDocument();
  });

  it('receives a submitted purchase order after confirmation', async () => {
    receivePurchaseOrder.mockResolvedValue({ ...SUBMITTED, status: 2 });

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Receive PO-2026-0002' }),
    );

    await waitFor(() => {
      expect(receivePurchaseOrder).toHaveBeenCalledWith('test-token', SUBMITTED.id);
    });
    expect(await screen.findByText(/PO-2026-0002 received and stock updated/)).toBeInTheDocument();
  });

  it('cancels a draft purchase order after confirmation', async () => {
    cancelPurchaseOrder.mockResolvedValue({ ...DRAFT, status: 3 });

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Cancel PO-2026-0001' }),
    );

    await waitFor(() => {
      expect(cancelPurchaseOrder).toHaveBeenCalledWith('test-token', DRAFT.id);
    });
    expect(await screen.findByText('Purchase order PO-2026-0001 cancelled.')).toBeInTheDocument();
  });

  it('does nothing when the confirmation is dismissed', async () => {
    window.confirm.mockReturnValue(false);

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Submit PO-2026-0001' }),
    );

    expect(submitPurchaseOrder).not.toHaveBeenCalled();
  });

  it('surfaces a 409 conflict when submitting is not allowed', async () => {
    submitPurchaseOrder.mockRejectedValue(
      Object.assign(new Error('Transition from \'Received\' to \'Submitted\' is not allowed.'), {
        status: 409,
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Submit PO-2026-0001' }),
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Transition from 'Received' to 'Submitted' is not allowed.",
    );
  });

  it('surfaces a 403 when the user may not receive', async () => {
    receivePurchaseOrder.mockRejectedValue(
      Object.assign(new Error('Forbidden'), { status: 403 }),
    );

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Receive PO-2026-0002' }),
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /permission to receive purchase orders/i,
    );
  });

  it('surfaces a 409 conflict when cancelling is not allowed', async () => {
    cancelPurchaseOrder.mockRejectedValue(
      Object.assign(new Error('Purchase order is already \'Cancelled\'.'), { status: 409 }),
    );

    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole('button', { name: 'Cancel PO-2026-0001' }),
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Purchase order is already 'Cancelled'.",
    );
  });

  it('signs the user out when the session has expired', async () => {
    getPurchaseOrders.mockRejectedValue(
      Object.assign(new Error('Unauthorized'), { status: 401, data: null }),
    );

    renderPage();

    await waitFor(() => {
      expect(mockAuth.logout).toHaveBeenCalled();
    });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
