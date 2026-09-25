import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createSupplier,
  deleteSupplier,
  getSuppliers,
  updateSupplier,
} from '../../services/catalogApi';
import { SuppliersPage } from './SuppliersPage';

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ token: 'test-token' }),
}));

vi.mock('../../services/catalogApi', () => ({
  getSuppliers: vi.fn(),
  createSupplier: vi.fn(),
  updateSupplier: vi.fn(),
  deleteSupplier: vi.fn(),
}));

const ATLAS = {
  id: 'supplier-1',
  name: 'Atlas Textiles',
  contactName: 'Nimal Perera',
  email: 'orders@atlastextiles.lk',
  phone: '+94 11 234 5678',
  isActive: true,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-02T00:00:00Z',
};

const NORDIC = {
  id: 'supplier-2',
  name: 'Nordic Footwear',
  contactName: 'Kamal Silva',
  email: 'sales@nordicfootwear.lk',
  phone: '+94 11 876 1122',
  isActive: false,
  createdAt: '2026-09-03T00:00:00Z',
  updatedAt: '2026-09-04T00:00:00Z',
};

function renderSuppliersPage() {
  return render(
    <MemoryRouter initialEntries={['/suppliers']}>
      <SuppliersPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  getSuppliers.mockResolvedValue([ATLAS, NORDIC]);
  createSupplier.mockResolvedValue(ATLAS);
  updateSupplier.mockResolvedValue(ATLAS);
  deleteSupplier.mockResolvedValue(undefined);
});

describe('SuppliersPage', () => {
  it('shows a loading state, then the suppliers', async () => {
    renderSuppliersPage();

    expect(screen.getByRole('status')).toHaveTextContent('Loading suppliers');

    expect(await screen.findByText('Atlas Textiles')).toBeInTheDocument();
    expect(screen.getByText('Nordic Footwear')).toBeInTheDocument();
    expect(screen.getByText('orders@atlastextiles.lk')).toBeInTheDocument();
    expect(screen.getByText('Active', { selector: '.status-pill' })).toBeInTheDocument();
    expect(screen.getByText('Inactive', { selector: '.status-pill' })).toBeInTheDocument();
  });

  it('shows an empty state when no suppliers exist', async () => {
    getSuppliers.mockResolvedValue([]);

    renderSuppliersPage();

    expect(
      await screen.findByText('No suppliers matched the current filters.'),
    ).toBeInTheDocument();
  });

  it('shows an error state with retry', async () => {
    const user = userEvent.setup();
    getSuppliers
      .mockRejectedValueOnce({ status: 500, message: 'Server unavailable.' })
      .mockResolvedValue([ATLAS]);

    renderSuppliersPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The server could not complete the request. Please try again later.',
    );

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Atlas Textiles')).toBeInTheDocument();
  });

  it('creates a supplier through the form', async () => {
    const user = userEvent.setup();

    renderSuppliersPage();
    await screen.findByText('Atlas Textiles');

    await user.click(screen.getByRole('button', { name: 'Create supplier' }));
    await user.type(screen.getByLabelText('Supplier name'), 'Loom & Thread');
    await user.click(screen.getByRole('button', { name: 'Save supplier' }));

    expect(createSupplier).toHaveBeenCalledWith(
      {
        name: 'Loom & Thread',
        contactName: null,
        email: null,
        phone: null,
        isActive: true,
      },
      { token: 'test-token' },
    );
    expect(
      await screen.findByText('Supplier created successfully.'),
    ).toBeInTheDocument();
  });

  it('updates a supplier through the form', async () => {
    const user = userEvent.setup();

    renderSuppliersPage();
    await screen.findByText('Atlas Textiles');

    await user.click(screen.getAllByRole('button', { name: 'Edit' })[0]);
    await user.clear(screen.getByLabelText('Phone'));
    await user.type(screen.getByLabelText('Phone'), '+94 11 000 0000');
    await user.click(screen.getByRole('button', { name: 'Save supplier' }));

    expect(updateSupplier).toHaveBeenCalledWith(
      ATLAS.id,
      {
        name: 'Atlas Textiles',
        contactName: 'Nimal Perera',
        email: 'orders@atlastextiles.lk',
        phone: '+94 11 000 0000',
        isActive: true,
      },
      { token: 'test-token' },
    );
    expect(
      await screen.findByText('Supplier updated successfully.'),
    ).toBeInTheDocument();
  });

  it('deactivates a supplier after confirmation', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderSuppliersPage();
    await screen.findByText('Atlas Textiles');

    await user.click(
      screen.getAllByRole('button', { name: 'Deactivate' })[0],
    );

    expect(window.confirm).toHaveBeenCalled();
    expect(deleteSupplier).toHaveBeenCalledWith('supplier-1', {
      token: 'test-token',
    });
    expect(
      await screen.findByText('Supplier deactivated successfully.'),
    ).toBeInTheDocument();
  });

  it('filters suppliers by search text on the client', async () => {
    const user = userEvent.setup();

    renderSuppliersPage();
    await screen.findByText('Atlas Textiles');

    await user.type(screen.getByLabelText('Search suppliers'), 'nordic');

    expect(screen.getByText('Nordic Footwear')).toBeInTheDocument();
    expect(screen.queryByText('Atlas Textiles')).not.toBeInTheDocument();
  });
});
