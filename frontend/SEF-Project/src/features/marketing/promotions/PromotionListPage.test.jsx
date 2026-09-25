import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import { getCampaigns } from '../../../services/campaignService';
import { getPromotions, updatePromotion } from '../../../services/promotionService';
import PromotionListPage from './PromotionListPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/campaignService', () => ({ getCampaigns: vi.fn() }));
vi.mock('../../../services/promotionService', () => ({
  getPromotions: vi.fn(),
  updatePromotion: vi.fn(),
}));

const promotion = {
  id: 'promo-1',
  name: 'Pizza 20% Off',
  description: null,
  type: 0,
  discountValue: 20,
  startDate: '2026-09-01T00:00:00Z',
  endDate: '2099-12-31T00:00:00Z',
  isActive: true,
  campaignId: 'camp-1',
  campaignName: 'Summer Launch',
  productIds: ['p1'],
  categoryIds: [],
};

function page(items, totalCount = items.length) {
  return { items, totalCount, page: 1, pageSize: 10 };
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/promotions']}>
      <PromotionListPage />
    </MemoryRouter>
  );
}

describe('PromotionListPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getCampaigns).mockResolvedValue(page([{ id: 'camp-1', name: 'Summer Launch' }]));
    vi.mocked(getPromotions).mockReset();
    vi.mocked(updatePromotion).mockReset();
  });

  it('shows a loading state, then the promotions', async () => {
    vi.mocked(getPromotions).mockResolvedValue(page([promotion]));

    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Loading promotions');
    expect(await screen.findByRole('link', { name: 'Pizza 20% Off' })).toHaveAttribute(
      'href',
      '/marketing/promotions/promo-1'
    );
    expect(screen.getByText('20% off')).toBeInTheDocument();
    expect(screen.getByText('Running')).toBeInTheDocument();
    expect(getPromotions).toHaveBeenCalledWith(
      'token-1',
      expect.objectContaining({ page: 1, pageSize: 10, sortBy: 'startDate' })
    );
  });

  it('shows an empty state', async () => {
    vi.mocked(getPromotions).mockResolvedValue(page([]));

    renderPage();

    expect(await screen.findByText('No promotions yet.')).toBeInTheDocument();
  });

  it('shows an error state with retry', async () => {
    const user = userEvent.setup();
    vi.mocked(getPromotions)
      .mockRejectedValueOnce({ status: 500, message: 'Server unavailable.' })
      .mockResolvedValue(page([promotion]));

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server unavailable.');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: 'Pizza 20% Off' })).toBeInTheDocument();
  });

  it('searches and filters through the API', async () => {
    const user = userEvent.setup();
    vi.mocked(getPromotions).mockResolvedValue(page([promotion]));

    renderPage();
    await screen.findByRole('link', { name: 'Pizza 20% Off' });

    await user.type(screen.getByLabelText('Search by name'), 'pizza');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() =>
      expect(getPromotions).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ search: 'pizza', page: 1 })
      )
    );

    await user.selectOptions(screen.getByLabelText('Active'), 'false');

    await waitFor(() =>
      expect(getPromotions).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ search: 'pizza', isActive: 'false' })
      )
    );
  });

  it('deactivates a promotion and reports success', async () => {
    const user = userEvent.setup();
    vi.mocked(getPromotions).mockResolvedValue(page([promotion]));
    vi.mocked(updatePromotion).mockResolvedValue({ ...promotion, isActive: false });

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Deactivate Pizza 20% Off' }));

    expect(updatePromotion).toHaveBeenCalledWith(
      'token-1',
      'promo-1',
      expect.objectContaining({ isActive: false, name: 'Pizza 20% Off', discountValue: 20 })
    );
    expect(await screen.findByText('"Pizza 20% Off" deactivated.')).toBeInTheDocument();
  });
});
