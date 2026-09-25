import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { CartProvider } from '../cart/CartContext';
import { StorefrontPage } from './StorefrontPage';
import { getStorefrontProducts } from './storefrontService';

vi.mock('./storefrontService', () => ({
  getStorefrontProducts: vi.fn(),
}));

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const PRODUCTS = [
  {
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
    variants: [],
    inStock: true,
  },
  {
    id: 'product-2',
    name: 'Leather Ankle Boots',
    description: 'Full-grain leather boots.',
    imageUrl: null,
    categoryId: 'category-footwear',
    categoryName: 'Footwear',
    collectionName: 'Signature Selection',
    priceFrom: 8900,
    priceTo: 8900,
    sizes: ['One Size'],
    colours: [{ name: 'Black', hexCode: '#000000' }],
    variants: [],
    inStock: true,
  },
];

function LocationProbe() {
  const location = useLocation();

  return <span data-testid="location">{location.pathname}</span>;
}

function renderStorefront() {
  return render(
    <CartProvider>
      <MemoryRouter initialEntries={['/']}>
        <LocationProbe />
        <Routes>
          <Route path="/" element={<StorefrontPage />} />
          <Route path="/shop/:id" element={<p>Product detail</p>} />
          <Route path="/login" element={<p>Login page</p>} />
          <Route path="/cart" element={<p>Cart page</p>} />
        </Routes>
      </MemoryRouter>
    </CartProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();

  mockAuth = {
    isAuthenticated: false,
    isStaffOrAdmin: false,
    user: null,
    logout: vi.fn(),
  };

  getStorefrontProducts.mockResolvedValue(PRODUCTS);
});

describe('StorefrontPage', () => {
  it('shows the catalogue with prices and product details', async () => {
    renderStorefront();

    expect(
      await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Leather Ankle Boots' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Tops · Summer Essentials')).toBeInTheDocument();
    expect(screen.getByText('From LKR 2,500.00')).toBeInTheDocument();
    expect(screen.getByText('LKR 8,900.00')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Everyday essentials, made to last.' }),
    ).toBeInTheDocument();
  });

  it('resolves a relative product image against the API origin', async () => {
    getStorefrontProducts.mockResolvedValue([
      {
        ...PRODUCTS[0],
        imageUrl: '/images/products/classic-cotton-tshirt.svg',
      },
    ]);

    const { container } = renderStorefront();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    expect(container.querySelector('.product-card__image')).toHaveAttribute(
      'src',
      'http://localhost:5193/images/products/classic-cotton-tshirt.svg',
    );
  });

  it('filters the grid by category', async () => {
    const user = userEvent.setup();

    renderStorefront();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getByRole('button', { name: 'Footwear' }));

    expect(
      screen.queryByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Leather Ankle Boots' }),
    ).toBeInTheDocument();
  });

  it('shows an empty state when the catalogue has no products', async () => {
    getStorefrontProducts.mockResolvedValue([]);

    renderStorefront();

    expect(
      await screen.findByText('No products are available yet — check back soon.'),
    ).toBeInTheDocument();
  });

  it('shows an error with retry when the catalogue fails to load', async () => {
    getStorefrontProducts
      .mockRejectedValueOnce(new Error('Catalogue is down'))
      .mockResolvedValueOnce(PRODUCTS);

    const user = userEvent.setup();
    renderStorefront();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Catalogue is down',
    );

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(
      await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
  });

  it('links each product to its detail page', async () => {
    const user = userEvent.setup();

    renderStorefront();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(
      screen.getByRole('link', { name: 'Classic Cotton T-Shirt' }),
    );

    expect(screen.getByText('Product detail')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/shop/product-1');
  });

  it('offers the cart and sign-in links to visitors', async () => {
    renderStorefront();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    expect(screen.getByRole('link', { name: 'Cart' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('hides the buy action for sold-out products', async () => {
    getStorefrontProducts.mockResolvedValue([
      { ...PRODUCTS[1], id: 'product-3', inStock: false },
    ]);

    renderStorefront();

    const card = (await screen.findByText('Sold out')).closest('.product-card');

    expect(
      within(card).queryByRole('link', { name: 'Choose options' }),
    ).not.toBeInTheDocument();
  });
});
