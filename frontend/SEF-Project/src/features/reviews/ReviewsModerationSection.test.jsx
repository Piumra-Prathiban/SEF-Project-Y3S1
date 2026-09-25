import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReviewsModerationSection } from './ReviewsModerationSection';
import {
  deleteReview,
  listReviewsForModeration,
  setReviewPublished,
} from './reviewService';

vi.mock('./reviewService', () => ({
  listReviewsForModeration: vi.fn(),
  setReviewPublished: vi.fn(),
  deleteReview: vi.fn(),
}));

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ token: 'staff-token', isAuthenticated: true }),
}));

const REVIEW = {
  id: 'review-1',
  productId: 'product-1',
  displayName: 'Asha P.',
  rating: 5,
  comment: 'Beautiful fit and finish.',
  isPublished: true,
  createdAt: '2026-09-01T10:00:00Z',
  updatedAt: '2026-09-01T10:00:00Z',
};

function moderationList(items) {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalItems: items.length,
    totalPages: 1,
  };
}

beforeEach(() => {
  vi.clearAllMocks();

  listReviewsForModeration.mockResolvedValue(moderationList([REVIEW]));
  setReviewPublished.mockResolvedValue({ ...REVIEW, isPublished: false });
  deleteReview.mockResolvedValue(undefined);

  vi.spyOn(window, 'confirm').mockReturnValue(true);
});

describe('ReviewsModerationSection', () => {
  it('lists reviews with reviewer, rating and published status', async () => {
    render(<ReviewsModerationSection />);

    expect(await screen.findByText('Asha P.')).toBeInTheDocument();
    expect(screen.getByText('Beautiful fit and finish.')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Hide' })).toBeInTheDocument();
  });

  it('shows the empty state when no reviews match', async () => {
    listReviewsForModeration.mockResolvedValue(moderationList([]));

    render(<ReviewsModerationSection />);

    expect(
      await screen.findByText('No reviews match these filters.'),
    ).toBeInTheDocument();
  });

  it('shows the error and retries', async () => {
    listReviewsForModeration.mockRejectedValueOnce(
      Object.assign(new Error('Moderation unavailable'), { status: 500 }),
    );

    const user = userEvent.setup();
    render(<ReviewsModerationSection />);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Moderation unavailable',
    );

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Asha P.')).toBeInTheDocument();
  });

  it('hides a review and reflects the new state in the table', async () => {
    const user = userEvent.setup();
    render(<ReviewsModerationSection />);

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: 'Hide' }));

    await waitFor(() => {
      expect(setReviewPublished).toHaveBeenCalledWith(
        'staff-token',
        'review-1',
        false,
      );
    });

    expect(await screen.findByText('Hidden')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Unhide' }),
    ).toBeInTheDocument();
    expect(
      await screen.findByText('Review hidden from the storefront.'),
    ).toBeInTheDocument();
  });

  it('unhides a hidden review', async () => {
    listReviewsForModeration.mockResolvedValue(
      moderationList([{ ...REVIEW, isPublished: false }]),
    );
    setReviewPublished.mockResolvedValue({ ...REVIEW, isPublished: true });

    const user = userEvent.setup();
    render(<ReviewsModerationSection />);

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: 'Unhide' }));

    await waitFor(() => {
      expect(setReviewPublished).toHaveBeenCalledWith(
        'staff-token',
        'review-1',
        true,
      );
    });

    expect(await screen.findByText('Published')).toBeInTheDocument();
  });

  it('deletes a review after confirmation', async () => {
    const user = userEvent.setup();
    render(<ReviewsModerationSection />);

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: 'Delete' }));

    await waitFor(() => {
      expect(deleteReview).toHaveBeenCalledWith('staff-token', 'review-1');
    });

    expect(window.confirm).toHaveBeenCalled();
    expect(
      await screen.findByText('No reviews match these filters.'),
    ).toBeInTheDocument();
  });

  it('does not delete when the confirmation is declined', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    const user = userEvent.setup();
    render(<ReviewsModerationSection />);

    await screen.findByText('Asha P.');

    await user.click(screen.getByRole('button', { name: 'Delete' }));

    expect(deleteReview).not.toHaveBeenCalled();
    expect(screen.getByText('Asha P.')).toBeInTheDocument();
  });
});
