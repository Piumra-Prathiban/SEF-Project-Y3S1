import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { CartProvider } from './CartContext';
import { CartPage } from './CartPage';

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const ITEM = {
  variantId: 'variant-xs-black',
  productId: 'product-1',
  productName: 'Classic Cotton T-Shirt',
  imageUrl: null,
  sizeName: 'XS',
  colourName: 'Black',
  price: 2500,
  quantity: 2,
};

function LocationProbe() {
  const location = useLocation();

  return <span data-testid="location">{location.pathname}</span>;
}

function renderCart() {
  return render(
    <CartProvider>
      <MemoryRouter initialEntries={['/cart']}>
        <LocationProbe />
        <Routes>
          <Route path="/cart" element={<CartPage />} />
          <Route path="/checkout" element={<p>Checkout page</p>} />
          <Route path="/login" element={<p>Login page</p>} />
        </Routes>
      </MemoryRouter>
    </CartProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();

  mockAuth = {
    isAuthenticated: false,
    isStaffOrAdmin: false,
    token: null,
    user: null,
    logout: vi.fn(),
  };
});

describe('CartPage', () => {
  it('lists the cart items with an estimated subtotal', async () => {
    window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));

    renderCart();

    expect(
      await screen.findByRole('link', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
    expect(screen.getByText('XS · Black')).toBeInTheDocument();
    const summary = screen
      .getByText(/Estimated subtotal \(2 items\)/)
      .closest('.cart-summary');

    expect(within(summary).getByText('LKR 5,000.00')).toBeInTheDocument();
  });

  it('recalculates the subtotal when the quantity changes', async () => {
    window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));

    const user = userEvent.setup();
    renderCart();

    await screen.findByRole('link', { name: 'Classic Cotton T-Shirt' });

    await user.clear(screen.getByLabelText('Qty'));
    await user.type(screen.getByLabelText('Qty'), '3');

    const summary = screen
      .getByText(/Estimated subtotal \(3 items\)/)
      .closest('.cart-summary');

    expect(within(summary).getByText('LKR 7,500.00')).toBeInTheDocument();
  });

  it('removes an item and shows the empty state', async () => {
    window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));

    const user = userEvent.setup();
    renderCart();

    await screen.findByRole('link', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Remove' }));

    expect(screen.getByText('Your cart is empty.')).toBeInTheDocument();
  });

  it('shows the empty state for an empty cart', async () => {
    renderCart();

    expect(await screen.findByText('Your cart is empty.')).toBeInTheDocument();
  });

  it('sends anonymous shoppers to sign in at checkout', async () => {
    window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));

    const user = userEvent.setup();
    renderCart();

    await screen.findByRole('link', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Proceed to checkout' }));

    expect(screen.getByText('Login page')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/login');
  });

  it('sends signed-in shoppers to the checkout', async () => {
    mockAuth = {
      ...mockAuth,
      isAuthenticated: true,
      token: 'test-token',
      user: { email: 'shopper@example.com', role: 'Customer' },
    };
    window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));

    const user = userEvent.setup();
    renderCart();

    await screen.findByRole('link', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Proceed to checkout' }));

    expect(screen.getByText('Checkout page')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/checkout');
  });
});
