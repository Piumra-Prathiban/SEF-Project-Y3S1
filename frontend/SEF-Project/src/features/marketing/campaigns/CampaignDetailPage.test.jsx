import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FlashMessage from '../../../components/FlashMessage';
import { useAuth } from '../../../contexts/AuthContext';
import { deleteCampaign, getCampaign, updateCampaign } from '../../../services/campaignService';
import { getPromotions } from '../../../services/promotionService';
import { CAMPAIGN_STATUSES } from '../marketingConstants';
import CampaignDetailPage from './CampaignDetailPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/campaignService', () => ({
  getCampaign: vi.fn(),
  updateCampaign: vi.fn(),
  deleteCampaign: vi.fn(),
}));
vi.mock('../../../services/promotionService', () => ({ getPromotions: vi.fn() }));

const activeCampaign = {
  id: 'camp-1',
  name: 'Summer Launch',
  description: 'Launch promotion for the new menu.',
  startDate: '2026-09-01T00:00:00Z',
  endDate: '2026-12-31T00:00:00Z',
  status: CAMPAIGN_STATUSES.ACTIVE,
  updatedAt: '2026-09-01T00:00:00Z',
};

const linkedPromotion = {
  id: 'promo-1',
  name: 'Pizza 20% Off',
  type: 0,
  discountValue: 20,
  startDate: '2026-09-01T00:00:00Z',
  endDate: '2099-12-31T00:00:00Z',
  isActive: true,
};

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/campaigns/camp-1']}>
      <Routes>
        <Route path="/marketing/campaigns/:id" element={<CampaignDetailPage />} />
        <Route path="/marketing/campaigns" element={<FlashMessage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('CampaignDetailPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getCampaign).mockReset();
    vi.mocked(updateCampaign).mockReset();
    vi.mocked(deleteCampaign).mockReset();
    vi.mocked(getPromotions).mockReset().mockResolvedValue({ items: [linkedPromotion] });
  });

  it('shows a loading state, then the campaign with its linked promotions', async () => {
    vi.mocked(getCampaign).mockResolvedValue(activeCampaign);

    renderPage();

    expect(screen.getByText('Loading campaign…')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Summer Launch' })).toBeInTheDocument();
    expect(within(document.querySelector('.details')).getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Promotions (1)')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pizza 20% Off' })).toHaveAttribute(
      'href',
      '/marketing/promotions/promo-1'
    );
  });

  it('shows an empty state when the campaign has no promotions', async () => {
    vi.mocked(getCampaign).mockResolvedValue(activeCampaign);
    vi.mocked(getPromotions).mockResolvedValue({ items: [] });

    renderPage();

    expect(await screen.findByText('This campaign has no promotions yet.')).toBeInTheDocument();
  });

  it('shows an error state', async () => {
    vi.mocked(getCampaign).mockRejectedValue({ status: 404, message: 'Not Found' });

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('The requested item was not found.');
  });

  it('changes status and reports success', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaign).mockResolvedValue(activeCampaign);
    vi.mocked(updateCampaign).mockResolvedValue({ ...activeCampaign, status: CAMPAIGN_STATUSES.PAUSED });

    renderPage();
    await screen.findByRole('heading', { name: 'Summer Launch' });

    await user.selectOptions(screen.getByLabelText('Change status'), String(CAMPAIGN_STATUSES.PAUSED));
    await user.click(screen.getByRole('button', { name: 'Update status' }));

    expect(updateCampaign).toHaveBeenCalledWith(
      'token-1',
      'camp-1',
      expect.objectContaining({ status: CAMPAIGN_STATUSES.PAUSED })
    );
    expect(await screen.findByText('Status changed to Paused.')).toBeInTheDocument();
  });

  it('locks the status control for a completed campaign', async () => {
    vi.mocked(getCampaign).mockResolvedValue({ ...activeCampaign, status: CAMPAIGN_STATUSES.COMPLETED });

    renderPage();
    await screen.findByRole('heading', { name: 'Summer Launch' });

    expect(screen.getByLabelText('Change status')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Update status' })).toBeDisabled();
    expect(screen.getByText('Completed and cancelled campaigns cannot change status.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Add promotion' })).not.toBeInTheDocument();
  });

  it('asks for confirmation, then deletes and shows success', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaign).mockResolvedValue(activeCampaign);
    vi.mocked(deleteCampaign).mockResolvedValue(null);

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Delete' }));

    expect(deleteCampaign).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Yes, delete' }));

    expect(deleteCampaign).toHaveBeenCalledWith('token-1', 'camp-1');
    expect(await screen.findByText('Campaign "Summer Launch" deleted.')).toBeInTheDocument();
  });

  it('explains why a delete was refused', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaign).mockResolvedValue(activeCampaign);
    vi.mocked(deleteCampaign).mockRejectedValue({
      status: 409,
      message: 'Campaign has promotions and cannot be deleted. Cancel it instead.',
    });

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Delete' }));
    await user.click(screen.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Campaign has promotions and cannot be deleted.'
    );
  });
});
