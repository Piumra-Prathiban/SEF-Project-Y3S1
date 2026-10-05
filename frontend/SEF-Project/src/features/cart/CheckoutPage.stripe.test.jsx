import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CartProvider } from './CartContext';
import { CheckoutPage } from './CheckoutPage';
import { createOrder } from '../../services/orderService';
import { getAddresses, getProfile } from '../profile/profileService';

const { confirmPayment } = vi.hoisted(() => ({ confirmPayment: vi.fn() }));

vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }) => children,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: () => ({ confirmPayment }),
  useElements: () => ({}),
}));

vi.mock('./stripe', () => ({ stripePromise: Promise.resolve({}) }));

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    createOrder: vi.fn(),
  };
});

vi.mock('../profile/profileService', () => ({
  getProfile: vi.fn(),
  getAddresses: vi.fn(),
}));

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

const ORDER_WITH_CARD = {
  id: 'order-1',
  orderNumber: 'ORD-1001',
  total: 5000,
  currency: 'LKR',
  payments: [{ id: 'payment-1', amount: 5000, status: 0, clientSecret: 'pi_test_secret' }],
};

function renderCheckout() {
  return render(
    <CartProvider>
      <MemoryRouter initialEntries={['/checkout']}>
        <CheckoutPage />
      </MemoryRouter>
    </CartProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();

  mockAuth = {
    isAuthenticated: true,
    isStaffOrAdmin: false,
    token: 'test-token',
    user: { email: 'shopper@example.com', role: 'Customer' },
    logout: vi.fn(),
  };

  window.localStorage.setItem('clothic.cart', JSON.stringify([ITEM]));
  getProfile.mockResolvedValue({ firstName: '', lastName: '' });
  getAddresses.mockResolvedValue([]);
  confirmPayment.mockResolvedValue({});
});

describe('CheckoutPage Stripe payment', () => {
  it('collects the card payment after the order is placed', async () => {
    createOrder.mockResolvedValue(ORDER_WITH_CARD);

    const user = userEvent.setup();
    renderCheckout();

    await screen.findByRole('heading', { name: 'Checkout' });
    await user.type(screen.getByLabelText('Full name'), 'Asha Perera');
    await user.type(screen.getByLabelText('Address line 1'), '12 Galle Road');
    await user.type(screen.getByLabelText('City'), 'Colombo');
    await user.type(screen.getByLabelText('Postal code'), '00300');
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(
      await screen.findByRole('heading', { name: 'Complete your payment' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('payment-element')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Pay/ }));

    await waitFor(() => {
      expect(confirmPayment).toHaveBeenCalled();
    });
    expect(
      await screen.findByRole('heading', { name: 'Order placed' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/card payment succeeded/)).toBeInTheDocument();
  });
});
