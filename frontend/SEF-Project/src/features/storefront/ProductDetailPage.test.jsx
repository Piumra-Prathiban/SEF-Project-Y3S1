import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { CartProvider } from '../cart/CartContext';
import { ProductDetailPage } from './ProductDetailPage';
import { getProductPromotions, getStorefrontProduct } from './storefrontService';

vi.mock('./storefrontService', () => ({
  getStorefrontProduct: vi.fn(),
  getProductPromotions: vi.fn(),
}));

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const PRODUCT = {
  id: 'product-1',
  name: 'Classic Cotton T-Shirt',
  description: 'Soft combed cotton crew-neck tee.',
  imageUrl: null,
  categoryId: 'category-tops',
  categoryName: 'Tops',
  collectionName: 'Summer Essentials',
  priceFrom: 2500,
  priceTo: 2600,
  sizes: ['XS', 'M'],
  colours: [
    { name: 'Black', hexCode: '#000000' },
    { name: 'White', hexCode: '#ffffff' },
  ],
  variants: [
    {
      id: 'variant-xs-black',
      sizeName: 'XS',
      colourName: 'Black',
      colourHex: '#000000',
      price: 2500,
      inStock: true,
    },
    {
      id: 'variant-m-white',
      sizeName: 'M',
      colourName: 'White',
      colourHex: '#ffffff',
      price: 2600,
      inStock: true,
    },
  ],
  inStock: true,
};

function LocationProbe() {
  const location = useLocation();

  return <span data-testid="location">{location.pathname}</span>;
}

function renderDetail() {
  return render(
    <CartProvider>
      <MemoryRouter initialEntries={['/shop/product-1']}>
        <LocationProbe />
        <Routes>
          <Route path="/shop/:id" element={<ProductDetailPage />} />
          <Route path="/cart" element={<p>Cart page</p>} />
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

  getStorefrontProduct.mockResolvedValue(PRODUCT);
  getProductPromotions.mockResolvedValue({
    promotions: [],
    variants: [],
  });
});

describe('ProductDetailPage', () => {
  it('renders the product with the first in-stock variant selected', async () => {
    renderDetail();

    expect(
      await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Tops · Summer Essentials')).toBeInTheDocument();
    expect(screen.getByText('LKR 2,500.00')).toBeInTheDocument();
    expect(screen.getByLabelText('Size')).toHaveValue('XS');
    expect(screen.getByRole('button', { name: /Black/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByText('In stock')).toBeInTheDocument();
  });

  it('resolves a relative product image against the API origin', async () => {
    getStorefrontProduct.mockResolvedValue({
      ...PRODUCT,
      imageUrl: '/images/products/classic-cotton-tshirt.svg',
    });

    const { container } = renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    expect(container.querySelector('.product-detail__media img')).toHaveAttribute(
      'src',
      'http://localhost:5193/images/products/classic-cotton-tshirt.svg',
    );
  });

  it('updates the colour options and price when the size changes', async () => {
    const user = userEvent.setup();

    renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.selectOptions(screen.getByLabelText('Size'), 'M');

    expect(screen.getByText('LKR 2,600.00')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /White/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(
      screen.queryByRole('button', { name: /Black/ }),
    ).not.toBeInTheDocument();
  });

  it('adds the selected variant to the cart', async () => {
    const user = userEvent.setup();

    renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Add to cart' }));

    expect(screen.getByRole('status')).toHaveTextContent(
      'added to your cart',
    );

    const stored = JSON.parse(window.localStorage.getItem('clothic.cart'));

    expect(stored).toHaveLength(1);
    expect(stored[0]).toMatchObject({
      variantId: 'variant-xs-black',
      productId: 'product-1',
      quantity: 1,
      price: 2500,
    });
  });

  it('shows the server-calculated promotion price and stores it in the cart', async () => {
    getProductPromotions.mockResolvedValue({
      promotions: [{ id: 'promotion-1', name: 'Tops 20% Off' }],
      variants: [{
        productVariantId: 'variant-xs-black',
        originalPrice: 2500,
        discountAmount: 500,
        finalPrice: 2000,
        promotionName: 'Tops 20% Off',
      }],
    });
    const user = userEvent.setup();

    renderDetail();

    expect(await screen.findByText('LKR 2,000.00')).toBeInTheDocument();
    expect(screen.getByText('LKR 2,500.00')).toHaveProperty('tagName', 'DEL');
    expect(screen.getByText(/applied automatically at checkout/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Add to cart' }));

    const stored = JSON.parse(window.localStorage.getItem('clothic.cart'));
    expect(stored[0].price).toBe(2000);
  });

  it('sends anonymous shoppers to sign in on Buy now', async () => {
    const user = userEvent.setup();

    renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Buy now' }));

    expect(screen.getByText('Login page')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/login');
  });

  it('sends signed-in shoppers to the cart on Buy now', async () => {
    mockAuth = {
      ...mockAuth,
      isAuthenticated: true,
      token: 'test-token',
      user: { email: 'shopper@example.com', role: 'Customer' },
    };

    const user = userEvent.setup();
    renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Buy now' }));

    expect(screen.getByText('Cart page')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/cart');
  });

  it('keeps customer purchase actions out of the staff storefront preview', async () => {
    mockAuth = {
      ...mockAuth,
      isAuthenticated: true,
      token: 'staff-token',
      user: { email: 'staff@example.com', role: 'Staff' },
    };

    renderDetail();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });
    expect(screen.getByText(/Staff accounts can preview the storefront/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add to cart' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Buy now' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save to wishlist' })).not.toBeInTheDocument();
  });

  it('shows a not-found state for a missing product', async () => {
    getStorefrontProduct.mockRejectedValue(
      Object.assign(new Error('Not found'), { status: 404 }),
    );

    renderDetail();

    expect(
      await screen.findByRole('heading', { name: 'Product not found' }),
    ).toBeInTheDocument();
  });
});
