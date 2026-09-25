import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { WishlistPage } from './WishlistPage';
import { getWishlist, removeWishlistItem } from './wishlistService';

vi.mock('./wishlistService', () => ({
  getWishlist: vi.fn(),
  removeWishlistItem: vi.fn(),
}));

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ token: 'test-token' }),
}));

const WISHLIST = {
  id: 'wishlist-1',
  items: [
    {
      productId: 'product-1',
      productName: 'Classic Cotton T-Shirt',
      description: 'Soft combed cotton crew-neck tee.',
      minimumPrice: 2500,
      isProductActive: true,
      isAvailable: true,
      addedAt: '2026-09-25T10:00:00Z',
    },
    {
      productId: 'product-2',
      productName: 'Leather Ankle Boots',
      description: null,
      minimumPrice: null,
      isProductActive: true,
      isAvailable: false,
      addedAt: '2026-09-25T10:05:00Z',
    },
  ],
  count: 2,
};

function renderWishlist() {
  return render(
    <MemoryRouter initialEntries={['/wishlist']}>
      <WishlistPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  getWishlist.mockResolvedValue(WISHLIST);
  removeWishlistItem.mockResolvedValue(undefined);
});

describe('WishlistPage', () => {
  it('lists saved pieces with availability and a link to the product', async () => {
    renderWishlist();

    expect(
      await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Leather Ankle Boots' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Available')).toBeInTheDocument();
    expect(screen.getByText('Unavailable')).toBeInTheDocument();
    expect(screen.getByText('From LKR 2,500.00')).toBeInTheDocument();
    expect(screen.getByText('No active variants')).toBeInTheDocument();

    const links = screen.getAllByRole('link', { name: 'Choose options' });

    expect(links[0]).toHaveAttribute('href', '/shop/product-1');
  });

  it('removes a saved piece', async () => {
    const user = userEvent.setup();

    renderWishlist();

    await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' });

    await user.click(screen.getAllByRole('button', { name: 'Remove' })[0]);

    expect(removeWishlistItem).toHaveBeenCalledWith('test-token', 'product-1');
    expect(
      screen.queryByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Leather Ankle Boots' }),
    ).toBeInTheDocument();
  });

  it('shows the empty state', async () => {
    getWishlist.mockResolvedValue({ id: null, items: [], count: 0 });

    renderWishlist();

    expect(await screen.findByText('Your wishlist is empty.')).toBeInTheDocument();
  });

  it('shows the API error and retries', async () => {
    getWishlist.mockRejectedValueOnce(
      Object.assign(new Error('Wishlist unavailable'), { status: 500 }),
    );

    const user = userEvent.setup();
    renderWishlist();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Wishlist unavailable',
    );

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(
      await screen.findByRole('heading', { name: 'Classic Cotton T-Shirt' }),
    ).toBeInTheDocument();
  });
});
