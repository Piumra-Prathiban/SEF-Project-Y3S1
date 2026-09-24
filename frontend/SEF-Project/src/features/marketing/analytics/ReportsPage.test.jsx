import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import {
  getDemandInsights,
  getInventoryStock,
  getProductPerformance,
  getPromotionPerformance,
  getSalesSummary,
} from '../../../services/analyticsService';
import { formatMoney } from '../marketingUtils';
import {
  demand,
  emptyStockPage,
  lowProducts,
  lowStockPage,
  noSalesProducts,
  outOfStockPage,
  promotionPerformance,
  summaryCurrent,
  summaryEmpty,
  summaryPrevious,
  topProducts,
  text,
} from './analyticsFixtures';
import ReportsPage from './ReportsPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/analyticsService', () => ({
  getSalesSummary: vi.fn(),
  getProductPerformance: vi.fn(),
  getInventoryStock: vi.fn(),
  getPromotionPerformance: vi.fn(),
  getDemandInsights: vi.fn(),
}));

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/reports']}>
      <ReportsPage />
    </MemoryRouter>
  );
}

const report = (name) => screen.findByRole('article', { name });

describe('ReportsPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getSalesSummary).mockReset().mockImplementation((token, query) =>
      Promise.resolve(new Date(query.to) > new Date() ? summaryCurrent : summaryPrevious)
    );
    vi.mocked(getProductPerformance).mockReset().mockImplementation((token, query) =>
      Promise.resolve(query.sortDirection === 'asc' ? lowProducts : topProducts)
    );
    vi.mocked(getInventoryStock).mockReset().mockImplementation((token, query) =>
      Promise.resolve(query.stockStatus === 2 ? outOfStockPage : lowStockPage)
    );
    vi.mocked(getPromotionPerformance).mockReset().mockResolvedValue(promotionPerformance);
    vi.mocked(getDemandInsights).mockReset().mockResolvedValue(demand);
  });

  it('summarises sales from the API figures', async () => {
    renderPage();

    const sales = await report('Sales summary');
    expect(sales).toHaveTextContent('2 orders');
    expect(sales).toHaveTextContent(text(formatMoney(5300)));
    expect(sales).toHaveTextContent(`average of ${text(formatMoney(2650))}`);
    expect(sales).toHaveTextContent('Revenue changed by +10.4%');
  });

  it('lists best sellers, unsold products, stock problems, promotions and demand changes', async () => {
    renderPage();

    const products = await report('Product performance');
    expect(within(products).getByText(/Pepperoni Pizza/)).toHaveTextContent(text(formatMoney(2600)));
    expect(products).toHaveTextContent('had no sales: Spaghetti Carbonara, Tiramisu.');

    const inventory = await report('Inventory status (current)');
    expect(inventory).toHaveTextContent('BEV-COLA-330');
    expect(inventory).toHaveTextContent('DES-TIRA-S');

    const promotions = await report('Promotions');
    expect(promotions).toHaveTextContent('3 promotions are live right now.');
    expect(promotions).toHaveTextContent('Pizza 20% Off: 2 redemptions by 1 customers');

    const demandReport = await report('Demand trends');
    expect(demandReport).toHaveTextContent('Cola (BEV-COLA-330): 1 → 3 units (+200.0%)');
    expect(demandReport).toHaveTextContent('Margherita Pizza (PIZ-MARG-S): 4 → 2 units (-50.0%)');
    expect(demandReport).not.toHaveTextContent('Tiramisu');
  });

  it('reports a quiet period honestly', async () => {
    vi.mocked(getSalesSummary).mockResolvedValue(summaryEmpty);
    vi.mocked(getProductPerformance).mockResolvedValue(noSalesProducts);
    vi.mocked(getInventoryStock).mockResolvedValue(emptyStockPage);
    vi.mocked(getDemandInsights).mockResolvedValue({ ...demand, items: [demand.items[2]] });

    renderPage();

    expect(await report('Sales summary')).toHaveTextContent('No sales were recorded between');
    expect(await report('Product performance')).toHaveTextContent('No products were sold in this period.');
    expect(await report('Inventory status (current)')).toHaveTextContent(
      'All active variants are above their reorder level.'
    );
    expect(await report('Demand trends')).toHaveTextContent('No variant changed demand by 10% or more');
  });

  it('shows an error with retry', async () => {
    const user = userEvent.setup();
    vi.mocked(getPromotionPerformance)
      .mockRejectedValueOnce({ status: 500, message: 'Reports unavailable.' })
      .mockResolvedValue(promotionPerformance);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Reports unavailable.');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await report('Sales summary')).toHaveTextContent('2 orders');
  });
});
