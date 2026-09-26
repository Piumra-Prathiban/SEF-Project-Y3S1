import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import { getCampaigns } from '../../../services/campaignService';
import { CAMPAIGN_STATUSES } from '../marketingConstants';
import CampaignListPage from './CampaignListPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/campaignService', () => ({ getCampaigns: vi.fn() }));

const campaign = {
  id: 'camp-1',
  name: 'Summer Launch',
  description: 'Launch promotion for the new menu.',
  startDate: '2026-09-01T00:00:00Z',
  endDate: '2026-12-31T00:00:00Z',
  status: CAMPAIGN_STATUSES.ACTIVE,
  promotionCount: 2,
};

function page(items, totalCount = items.length) {
  return { items, totalCount, page: 1, pageSize: 10 };
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/campaigns']}>
      <CampaignListPage />
    </MemoryRouter>
  );
}

describe('CampaignListPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getCampaigns).mockReset();
  });

  it('shows a loading state, then the campaigns', async () => {
    vi.mocked(getCampaigns).mockResolvedValue(page([campaign]));

    renderPage();

    expect(screen.getByText('Loading campaigns…')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Summer Launch' })).toHaveAttribute(
      'href',
      '/marketing/campaigns/camp-1'
    );
    const table = within(screen.getByRole('table'));
    expect(table.getByText('Active')).toBeInTheDocument();
    expect(table.getByText('2')).toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    vi.mocked(getCampaigns).mockResolvedValue(page([]));

    renderPage();

    expect(await screen.findByText('No campaigns yet.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create a campaign' })).toBeInTheDocument();
  });

  it('shows an error state with retry', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaigns)
      .mockRejectedValueOnce({ status: 500, message: 'Server unavailable.' })
      .mockResolvedValue(page([campaign]));

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server unavailable.');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: 'Summer Launch' })).toBeInTheDocument();
  });

  it('searches and filters by status through the API', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaigns).mockResolvedValue(page([campaign]));

    renderPage();
    await screen.findByRole('link', { name: 'Summer Launch' });

    await user.type(screen.getByLabelText('Search by name'), 'summer');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() =>
      expect(getCampaigns).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ search: 'summer', page: 1 })
      )
    );

    await user.selectOptions(screen.getByLabelText('Status'), String(CAMPAIGN_STATUSES.SCHEDULED));

    await waitFor(() =>
      expect(getCampaigns).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ search: 'summer', status: String(CAMPAIGN_STATUSES.SCHEDULED) })
      )
    );
  });

  it('clears filters and returns to the unfiltered empty state', async () => {
    const user = userEvent.setup();
    vi.mocked(getCampaigns).mockResolvedValue(page([]));

    renderPage();
    await user.type(screen.getByLabelText('Search by name'), 'nothing-matches');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByText('No campaigns match these filters.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));

    await waitFor(() =>
      expect(getCampaigns).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ search: '', status: '' })
      )
    );
  });
});
