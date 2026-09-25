import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FlashMessage from '../../../components/FlashMessage';
import { useAuth } from '../../../contexts/AuthContext';
import {
  deletePromotion,
  getPromotion,
  getPromotionTargets,
} from '../../../services/promotionService';
import PromotionDetailPage from './PromotionDetailPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/promotionService', () => ({
  deletePromotion: vi.fn(),
  getPromotion: vi.fn(),
  getPromotionTargets: vi.fn(),
  updatePromotion: vi.fn(),
}));

const promotion = {
  id: 'promo-1',
  name: 'Cola Rs. 50 Off',
  description: 'Rs. 50 off each cola.',
  type: 1,
  discountValue: 50,
  startDate: '2027-01-01T00:00:00Z',
  endDate: '2027-03-31T00:00:00Z',
  isActive: true,
  campaignId: null,
  campaignName: null,
  productIds: ['p2'],
  categoryIds: [],
  updatedAt: '2026-09-23T00:00:00Z',
};

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/promotions/promo-1']}>
      <Routes>
        <Route path="/marketing/promotions/:id" element={<PromotionDetailPage />} />
        <Route path="/marketing/promotions" element={<FlashMessage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('PromotionDetailPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getPromotion).mockResolvedValue(promotion);
    vi.mocked(getPromotionTargets).mockResolvedValue({
      products: [{ id: 'p2', name: 'Cola', isActive: true }],
      categories: [],
    });
    vi.mocked(deletePromotion).mockReset();
  });

  it('shows the promotion details with target names', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: 'Cola Rs. 50 Off' })).toBeInTheDocument();
    expect(screen.getByText('Fixed amount discount')).toBeInTheDocument();
    expect(screen.getByText('Cola')).toBeInTheDocument();
    expect(screen.getByText('Upcoming')).toBeInTheDocument();
  });

  it('asks for confirmation, deletes and shows success', async () => {
    const user = userEvent.setup();
    vi.mocked(deletePromotion).mockResolvedValue(null);

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Delete' }));

    expect(deletePromotion).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Yes, delete' }));

    expect(deletePromotion).toHaveBeenCalledWith('token-1', 'promo-1');
    expect(
      await screen.findByText('Promotion "Cola Rs. 50 Off" deleted.')
    ).toBeInTheDocument();
  });

  it('explains why a delete was refused', async () => {
    const user = userEvent.setup();
    vi.mocked(deletePromotion).mockRejectedValue({
      status: 409,
      message: 'Promotion has coupons and cannot be deleted. Deactivate it instead.',
    });

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Delete' }));
    await user.click(screen.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Promotion has coupons and cannot be deleted.'
    );
  });

  it('shows not found for a missing promotion', async () => {
    vi.mocked(getPromotion).mockRejectedValue({ status: 404, message: 'Not Found' });

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The requested item was not found.'
    );
  });
});
