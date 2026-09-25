import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CartProvider } from './CartContext';
import { CheckoutPage } from './CheckoutPage';
import { createOrder, createPayment, PaymentMethod } from '../../services/orderService';

vi.mock('../../services/orderService', async (importOriginal) => {
  const actual = await importOriginal();

  return {
    ...actual,
    createOrder: vi.fn(),
    createPayment: vi.fn(),
  };
});

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

const PLACED_ORDER = {
  id: 'order-1',
  orderNumber: 'ORD-1001',
  total: 5000,
  currency: 'LKR',
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

async function fillAddress(user) {
  await user.type(screen.getByLabelText('Full name'), 'Asha Perera');
  await user.type(screen.getByLabelText('Address line 1'), '12 Galle Road');
  await user.type(screen.getByLabelText('City'), 'Colombo');
  await user.type(screen.getByLabelText('Postal code'), '00300');
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
  createPayment.mockResolvedValue({ id: 'payment-1' });
});

describe('CheckoutPage', () => {
  it('places the order and shows a confirmation', async () => {
    createOrder.mockResolvedValue(PLACED_ORDER);

    const user = userEvent.setup();
    renderCheckout();

    await screen.findByRole('heading', { name: 'Checkout' });
    await fillAddress(user);
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    await waitFor(() => {
      expect(createOrder).toHaveBeenCalledWith('test-token', {
        items: [{ productVariantId: 'variant-xs-black', quantity: 2 }],
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
        paymentMethod: PaymentMethod.Card,
      });
    });

    expect(
      await screen.findByRole('heading', { name: 'Order placed' }),
    ).toBeInTheDocument();
    expect(screen.getByText('ORD-1001')).toBeInTheDocument();

    expect(createPayment).toHaveBeenCalledWith('test-token', 'order-1', {
      method: PaymentMethod.Card,
      amount: 5000,
    });

    expect(JSON.parse(window.localStorage.getItem('clothic.cart'))).toEqual([]);
  });

  it('keeps the order when the payment cannot be recorded', async () => {
    createOrder.mockResolvedValue(PLACED_ORDER);
    createPayment.mockRejectedValueOnce(
      Object.assign(new Error('Payment rejected'), { status: 409 }),
    );

    const user = userEvent.setup();
    renderCheckout();

    await screen.findByRole('heading', { name: 'Checkout' });
    await fillAddress(user);
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(
      await screen.findByRole('heading', { name: 'Order placed' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Your order is placed, but the payment could not be recorded.',
    );
    expect(JSON.parse(window.localStorage.getItem('clothic.cart'))).toEqual([]);
  });

  it('shows the backend message when the order cannot be placed', async () => {
    createOrder.mockRejectedValue(
      Object.assign(
        new Error('Insufficient stock for \'TSH-CLS-XS\'.'),
        { status: 409 },
      ),
    );

    const user = userEvent.setup();
    renderCheckout();

    await screen.findByRole('heading', { name: 'Checkout' });
    await fillAddress(user);
    await user.click(screen.getByRole('button', { name: 'Place order' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Insufficient stock for 'TSH-CLS-XS'.",
    );
    expect(
      screen.queryByRole('heading', { name: 'Order placed' }),
    ).not.toBeInTheDocument();

    expect(JSON.parse(window.localStorage.getItem('clothic.cart'))).toHaveLength(1);
  });

  it('tells the shopper there is nothing to check out when the cart is empty', async () => {
    window.localStorage.clear();

    renderCheckout();

    expect(
      await screen.findByText(
        'Your cart is empty, so there is nothing to check out.',
      ),
    ).toBeInTheDocument();
    expect(createOrder).not.toHaveBeenCalled();
  });
});
