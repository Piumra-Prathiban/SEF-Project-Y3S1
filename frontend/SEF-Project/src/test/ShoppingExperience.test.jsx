import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import App from '../App';
import { AuthProvider } from '../contexts/AuthContext';

function jsonResponse(data, status = 200) {
  return new Response(status === 204 ? null : JSON.stringify(data), {
    status,
    headers: status === 204 ? {} : { 'Content-Type': 'application/json' },
  });
}

function authenticate() {
  localStorage.setItem('sef-customer-session', JSON.stringify({
    token: 'test-jwt',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 7, email: 'customer@test.com', firstName: 'Test', role: 'Customer' },
  }));
}

function renderApp(path) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </AuthProvider>,
  );
}

const productResult = {
  page: 1,
  pageSize: 12,
  totalCount: 1,
  totalPages: 1,
  items: [{
    id: 'product-1',
    name: 'Cola',
    description: 'Cold sparkling drink',
    minimumPrice: 300,
    isAvailable: true,
    categories: [{ id: 'category-1', name: 'Beverages' }],
    variants: [{ id: 'variant-1', sku: 'BEV-COLA-330', name: '330ml', price: 300, availableQuantity: 10, isAvailable: true }],
  }],
};

it('provides the authentication context to React children', () => {
  expect(typeof AuthProvider).toBe('function');
  expect(typeof MemoryRouter).toBe('function');
  expect(typeof App).toBe('function');
  render(<AuthProvider><div>Context ready</div></AuthProvider>);
  expect(screen.getByText('Context ready')).toBeInTheDocument();
});

describe('shopping and customer experience', () => {
  it('sends product search and filter values to the backend', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(jsonResponse(productResult)));
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    renderApp('/products?search=drink');

    expect(await screen.findByRole('heading', { name: 'Cola' })).toBeInTheDocument();
    expect(fetchMock.mock.calls[0][0]).toContain('search=drink');

    const search = screen.getByRole('searchbox', { name: 'Search products' });
    await user.clear(search);
    await user.type(search, 'sparkling');
    await user.selectOptions(screen.getByLabelText('Sort by'), 'price:desc');
    await user.click(screen.getByRole('button', { name: 'Apply filters' }));

    await waitFor(() => {
      const requestedUrl = fetchMock.mock.calls.at(-1)[0];
      expect(requestedUrl).toContain('search=sparkling');
      expect(requestedUrl).toContain('sortBy=price');
      expect(requestedUrl).toContain('sortDirection=desc');
    });
  });

  it('redirects unauthenticated customers away from protected routes', async () => {
    vi.stubGlobal('fetch', vi.fn());
    renderApp('/cart');

    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email address')).toBeRequired();
  });

  it('updates cart quantity and displays backend-calculated totals', async () => {
    authenticate();
    const originalCart = {
      id: 'cart-1', currency: 'LKR', totalQuantity: 2, subtotal: 2400, total: 2400,
      items: [{ id: 'item-1', productVariantId: 'variant-1', productId: 'product-1', productName: 'Margherita Pizza', variantName: 'Small', sku: 'PIZ-MARG-S', unitPrice: 1200, quantity: 2, lineTotal: 2400, availableQuantity: 20, isAvailable: true, hasSufficientStock: true }],
    };
    const updatedCart = { ...originalCart, totalQuantity: 3, subtotal: 3600, total: 3600, items: [{ ...originalCart.items[0], quantity: 3, lineTotal: 3600 }] };
    const fetchMock = vi.fn((url, options = {}) => {
      if (options.method === 'PUT') return Promise.resolve(jsonResponse(updatedCart));
      return Promise.resolve(jsonResponse(originalCart));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    renderApp('/cart');

    const quantity = await screen.findByLabelText('Quantity for Margherita Pizza');
    await user.clear(quantity);
    await user.type(quantity, '3');
    await user.click(screen.getByRole('button', { name: 'Update' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/cart/items/item-1'),
      expect.objectContaining({ method: 'PUT', body: JSON.stringify({ quantity: 3 }) }),
    ));
    expect((await screen.findAllByText(/3,600\.00/)).length).toBeGreaterThan(0);
  });

  it('removes a saved product and shows the wishlist empty state', async () => {
    authenticate();
    const wishlist = {
      id: 'wishlist-1', count: 1,
      items: [{ productId: 'product-1', productName: 'Cola', description: 'Cold drink', minimumPrice: 300, isProductActive: true, isAvailable: true, addedAt: new Date().toISOString() }],
    };
    const fetchMock = vi.fn((url, options = {}) => Promise.resolve(
      options.method === 'DELETE' ? jsonResponse(null, 204) : jsonResponse(wishlist),
    ));
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    renderApp('/wishlist');

    await user.click(await screen.findByRole('button', { name: 'Remove' }));

    expect(await screen.findByRole('heading', { name: 'Your wishlist is empty' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/wishlist/items/product-1'),
      expect.objectContaining({ method: 'DELETE' }),
    );
  });

  it('creates a default address through the profile screen', async () => {
    authenticate();
    let addresses = [];
    const profile = { customerId: 4, email: 'customer@test.com', firstName: 'Test', lastName: 'Customer', memberSince: '2026-01-01T00:00:00Z' };
    const fetchMock = vi.fn((url, options = {}) => {
      if (url.endsWith('/profile/addresses') && options.method === 'POST') {
        const request = JSON.parse(options.body);
        addresses = [{ ...request, id: 9, isDefault: true }];
        return Promise.resolve(jsonResponse(addresses[0], 201));
      }
      if (url.endsWith('/profile/addresses')) return Promise.resolve(jsonResponse(addresses));
      return Promise.resolve(jsonResponse(profile));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    renderApp('/profile');

    await user.click(await screen.findByRole('button', { name: 'Add address' }));
    await user.type(screen.getByLabelText('Label'), 'Home');
    await user.type(screen.getByLabelText('Address line 1'), '10 Main Street');
    await user.type(screen.getByLabelText('City'), 'Kandy');
    await user.type(screen.getByLabelText('Postal code'), '20000');
    await user.click(screen.getByLabelText('Use as my default address'));
    await user.click(screen.getByRole('button', { name: 'Save address' }));

    expect(await screen.findByText('Default')).toBeInTheDocument();
    const postCall = fetchMock.mock.calls.find(([, options]) => options?.method === 'POST');
    expect(JSON.parse(postCall[1].body)).toEqual(expect.objectContaining({ label: 'Home', isDefault: true }));
  });
});
