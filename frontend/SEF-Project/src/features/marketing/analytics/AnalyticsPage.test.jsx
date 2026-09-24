import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import {
  getDemandInsights,
  getInventoryStock,
  getInventorySummary,
  getProductPerformance,
  getPromotionPerformance,
  getSalesOverTime,
} from '../../../services/analyticsService';
import { formatMoney } from '../marketingUtils';
import {
  demand,
  emptyStockPage,
  inventorySummary,
  lowProducts,
  lowStockPage,
  overTime,
  overTimeEmpty,
  promotionPerformance,
  topProducts,
  text,
} from './analyticsFixtures';
import AnalyticsPage from './AnalyticsPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/analyticsService', () => ({
  getSalesOverTime: vi.fn(),
  getProductPerformance: vi.fn(),
  getInventorySummary: vi.fn(),
  getInventoryStock: vi.fn(),
  getPromotionPerformance: vi.fn(),
  getDemandInsights: vi.fn(),
}));

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/analytics']}>
      <AnalyticsPage />
    </MemoryRouter>
  );
}

const section = (name) => screen.findByRole('region', { name });

describe('AnalyticsPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getSalesOverTime).mockReset().mockResolvedValue(overTime);
    vi.mocked(getProductPerformance).mockReset().mockImplementation((token, query) =>
      Promise.resolve(query.sortDirection === 'asc' ? lowProducts : topProducts)
    );
    vi.mocked(getInventorySummary).mockReset().mockResolvedValue(inventorySummary);
    vi.mocked(getInventoryStock).mockReset().mockResolvedValue(lowStockPage);
    vi.mocked(getPromotionPerformance).mockReset().mockResolvedValue(promotionPerformance);
    vi.mocked(getDemandInsights).mockReset().mockResolvedValue(demand);
  });

  it('renders all six analytics sections from API data', async () => {
    renderPage();

    const trend = await section('Revenue trend');
    expect(await within(trend).findByRole('img', { name: /Revenue over time/ })).toBeInTheDocument();

    const top = await section('Top selling products');
    expect(await within(top).findByText('Pepperoni Pizza')).toBeInTheDocument();

    const low = await section('Low performing products');
    expect(await within(low).findByText('Spaghetti Carbonara')).toBeInTheDocument();

    const inventory = await section('Inventory insights');
    expect(await within(inventory).findByText('DES-TIRA-S')).toBeInTheDocument();
    expect(within(inventory).getByText('Low stock', { selector: '.badge' })).toBeInTheDocument();

    const promotions = await section('Promotion performance');
    expect(await within(promotions).findByText('Pizza 20% Off')).toBeInTheDocument();
    expect(within(promotions).getByText(text(formatMoney(480)))).toBeInTheDocument();

    const demandSection = await section('Demand insights');
    expect(await within(demandSection).findByText('+200.0%')).toBeInTheDocument();
    expect(within(demandSection).getByText('Rising')).toBeInTheDocument();
    expect(within(demandSection).getByText('Falling')).toBeInTheDocument();
  });

  it('shows empty states when there is no data', async () => {
    vi.mocked(getSalesOverTime).mockResolvedValue(overTimeEmpty);
    vi.mocked(getProductPerformance).mockResolvedValue({
      items: [{ productId: 'p1', productName: 'Cola', unitsSold: 0, revenue: 0, orderCount: 0 }],
      totalCount: 1, page: 1, pageSize: 10,
    });
    vi.mocked(getPromotionPerformance).mockResolvedValue({
      ...promotionPerformance, items: [], totalCount: 0, livePromotionCount: 0, totalRedemptions: 0,
    });
    vi.mocked(getInventoryStock).mockResolvedValue(emptyStockPage);

    renderPage();

    expect(await screen.findByText('No sales in this period.')).toBeInTheDocument();
    expect(await screen.findByText('No products sold in this period.')).toBeInTheDocument();
    expect(await screen.findByText('No promotions yet.')).toBeInTheDocument();
    expect(await screen.findByText('No variants with this status.')).toBeInTheDocument();
  });

  it('keeps other sections working when one request fails', async () => {
    vi.mocked(getDemandInsights).mockRejectedValue({ status: 500, message: 'Demand query failed.' });

    renderPage();

    const demandSection = await section('Demand insights');
    expect(await within(demandSection).findByRole('alert')).toHaveTextContent('Demand query failed.');
    expect(await screen.findByText('Pizza 20% Off')).toBeInTheDocument();
  });

  it('passes section filters to the API', async () => {
    const user = userEvent.setup();
    renderPage();
    await within(await section('Inventory insights')).findByText('DES-TIRA-S');

    await user.selectOptions(screen.getByLabelText('Stock status'), 'Out of stock');
    await waitFor(() =>
      expect(getInventoryStock).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ stockStatus: '2', page: 1 })
      )
    );

    await user.selectOptions(screen.getByLabelText('Show'), 'Lowest demand');
    await waitFor(() =>
      expect(getDemandInsights).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ sortBy: 'unitsSold', sortDirection: 'asc' })
      )
    );

    await user.click(screen.getByLabelText('Live only'));
    await waitFor(() =>
      expect(getPromotionPerformance).toHaveBeenLastCalledWith(
        'token-1',
        expect.objectContaining({ liveOnly: true })
      )
    );
  });

  it('applies a custom date range to every section', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Pizza 20% Off');

    await user.selectOptions(screen.getByLabelText('Period'), 'Custom range');
    await user.clear(screen.getByLabelText('From'));
    await user.type(screen.getByLabelText('From'), '2026-10-01');
    await user.clear(screen.getByLabelText('To'));
    await user.type(screen.getByLabelText('To'), '2026-10-10');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    const expected = expect.objectContaining({
      from: '2026-10-01T00:00:00.000Z',
      to: '2026-10-11T00:00:00.000Z',
    });

    await waitFor(() => {
      expect(getSalesOverTime).toHaveBeenLastCalledWith('token-1', expected);
      expect(getPromotionPerformance).toHaveBeenLastCalledWith('token-1', expected);
      expect(getDemandInsights).toHaveBeenLastCalledWith('token-1', expected);
    });
  });
});
