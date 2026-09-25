import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ProductReviewsSection } from './ProductReviewsSection';
import {
  deleteOwnReview,
  getOwnReview,
  getProductReviews,
  saveProductReview,
} from './reviewService';

vi.mock('./reviewService', () => ({
  getProductReviews: vi.fn(),
  getOwnReview: vi.fn(),
  saveProductReview: vi.fn(),
  deleteOwnReview: vi.fn(),
}));

let mockAuth;

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

const PRODUCT_ID = 'product-1';

const REVIEWS = {
  productId: PRODUCT_ID,
  aggregate: {
    averageRating: 4.5,
    totalCount: 2,
    breakdown: {
      items: [
        { rating: 5, count: 1 },
        { rating: 4, count: 1 },
        { rating: 3, count: 0 },
        { rating: 2, count: 0 },
        { rating: 1, count: 0 },
      ],
    },
  },
  reviews: [
    {
      id: 'review-1',
      productId: PRODUCT_ID,
      displayName: 'Asha P.',
      rating: 5,
      comment: 'Beautiful fit and finish.',
      isPublished: true,
      createdAt: '2026-09-01T10:00:00Z',
      updatedAt: '2026-09-01T10:00:00Z',
    },
    {
      id: 'review-2',
      productId: PRODUCT_ID,
      displayName: 'Bimal S.',
      rating: 4,
      comment: null,
      isPublished: true,
      createdAt: '2026-08-20T10:00:00Z',
      updatedAt: '2026-08-20T10:00:00Z',
    },
  ],
};

function renderSection() {
  return render(
    <MemoryRouter>
      <ProductReviewsSection productId={PRODUCT_ID} />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();

  mockAuth = {
    user: { id: 1, email: 'customer@example.com', role: 'Customer' },
    token: 'test-token',
    isAuthenticated: true,
  };

  getProductReviews.mockResolvedValue(REVIEWS);
  getOwnReview.mockRejectedValue(
    Object.assign(new Error('Not found'), { status: 404 }),
  );
  saveProductReview.mockResolvedValue({ id: 'review-3', rating: 5 });
  deleteOwnReview.mockResolvedValue(undefined);
});

describe('ProductReviewsSection', () => {
  it('renders the aggregate rating, count and star breakdown', async () => {
    renderSection();

    expect(await screen.findByText('4.5')).toBeInTheDocument();
    expect(screen.getByText('2 reviews')).toBeInTheDocument();

    const breakdown = screen.getByRole('list', { name: 'Rating breakdown' });

    expect(within(breakdown).getAllByRole('listitem')).toHaveLength(5);
    expect(within(breakdown).getAllByText('1')).toHaveLength(2);
  });

  it('renders the published reviews with reviewer, rating and comment', async () => {
    renderSection();

    expect(await screen.findByText('Asha P.')).toBeInTheDocument();

    const list = screen.getByRole('list', { name: 'Customer reviews' });

    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
    expect(within(list).getByText('Beautiful fit and finish.')).toBeInTheDocument();
    expect(within(list).getByText('Bimal S.')).toBeInTheDocument();
  });

  it('shows the empty state when a product has no reviews', async () => {
    getProductReviews.mockResolvedValue({
      productId: PRODUCT_ID,
      aggregate: { averageRating: 0, totalCount: 0, breakdown: { items: [] } },
      reviews: [],
    });

    renderSection();

    expect(
      await screen.findByText(/No reviews yet\./),
    ).toBeInTheDocument();
  });

  it('shows the error and retries the request', async () => {
    getProductReviews.mockRejectedValueOnce(
      Object.assign(new Error('Reviews unavailable'), { status: 500 }),
    );

    const user = userEvent.setup();
    renderSection();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Reviews unavailable',
    );

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Asha P.')).toBeInTheDocument();
  });

  it('submits the rating and trimmed comment for a signed-in customer', async () => {
    const user = userEvent.setup();
    renderSection();

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: '5 stars' }));
    await user.type(
      screen.getByLabelText(/Your review/),
      'Great quality, true to size.',
    );
    await user.click(screen.getByRole('button', { name: 'Post review' }));

    await waitFor(() => {
      expect(saveProductReview).toHaveBeenCalledWith('test-token', PRODUCT_ID, {
        rating: 5,
        comment: 'Great quality, true to size.',
      });
    });

    expect(
      await screen.findByText('Thanks for reviewing this piece.'),
    ).toBeInTheDocument();
  });

  it('sends a null comment when the written review is left blank', async () => {
    const user = userEvent.setup();
    renderSection();

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: '4 stars' }));
    await user.click(screen.getByRole('button', { name: 'Post review' }));

    await waitFor(() => {
      expect(saveProductReview).toHaveBeenCalledWith('test-token', PRODUCT_ID, {
        rating: 4,
        comment: null,
      });
    });
  });

  it('offers the delete action once the customer has reviewed the product', async () => {
    getOwnReview.mockResolvedValue({
      id: 'review-1',
      productId: PRODUCT_ID,
      displayName: 'Asha P.',
      rating: 5,
      comment: 'Beautiful fit and finish.',
      isPublished: true,
      createdAt: '2026-09-01T10:00:00Z',
      updatedAt: '2026-09-01T10:00:00Z',
    });

    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const user = userEvent.setup();
    renderSection();

    const deleteButton = await screen.findByRole('button', {
      name: 'Delete my review',
    });

    expect(
      screen.getByRole('button', { name: 'Update review' }),
    ).toBeInTheDocument();

    await user.click(deleteButton);

    await waitFor(() => {
      expect(deleteOwnReview).toHaveBeenCalledWith('test-token', PRODUCT_ID);
    });

    expect(await screen.findByText('Your review was removed.')).toBeInTheDocument();
  });

  it('prompts anonymous visitors to sign in instead of showing the form', async () => {
    mockAuth = { user: null, token: null, isAuthenticated: false };

    renderSection();

    expect(await screen.findByText('Asha P.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute(
      'href',
      '/login',
    );
    expect(
      screen.queryByRole('button', { name: 'Post review' }),
    ).not.toBeInTheDocument();
    expect(getOwnReview).not.toHaveBeenCalled();
  });
});
