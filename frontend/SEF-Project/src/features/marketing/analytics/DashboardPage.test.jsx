import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuth } from '../../../contexts/AuthContext';
import {
  getProductPerformance,
  getSalesOverTime,
  getSalesSummary,
} from '../../../services/analyticsService';
import { formatMoney } from '../marketingUtils';
import {
  overTime,
  summaryCurrent,
  summaryEmpty,
  summaryPrevious,
  text,
  topProducts,
} from './analyticsFixtures';
import DashboardPage from './DashboardPage';

vi.mock('../../../contexts/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('../../../services/analyticsService', () => ({
  getSalesSummary: vi.fn(),
  getSalesOverTime: vi.fn(),
  getProductPerformance: vi.fn(),
}));

const DAY_MS = 24 * 60 * 60 * 1000;

// The current period ends after "now"; the comparison period ends before it.
function summaryFor(current, previous) {
  return (token, query) =>
    Promise.resolve(new Date(query.to) > new Date() ? current : previous);
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/marketing/dashboard']}>
      <DashboardPage />
    </MemoryRouter>
  );
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ token: 'token-1' });
    vi.mocked(getSalesSummary).mockReset().mockImplementation(summaryFor(summaryCurrent, summaryPrevious));
    vi.mocked(getSalesOverTime).mockReset().mockResolvedValue(overTime);
    vi.mocked(getProductPerformance).mockReset().mockResolvedValue(topProducts);
  });

  it('shows the four KPI cards from the API with change vs the previous period', async () => {
    renderPage();

    const kpis = await screen.findByRole('region', { name: 'Key figures' });

    await within(kpis).findByText(text(formatMoney(5300)));
    expect(within(kpis).getByText('Total revenue')).toBeInTheDocument();
    expect(within(kpis).getByText('Total orders')).toBeInTheDocument();
    expect(within(kpis).getByText(text(formatMoney(2650)))).toBeInTheDocument();
    expect(within(kpis).getByText('Units sold')).toBeInTheDocument();
    // 5300 vs 4800 revenue, 2 vs 1 orders.
    expect(within(kpis).getByText(/\+10\.4%/)).toBeInTheDocument();
    expect(within(kpis).getByText(/\+100\.0%/)).toBeInTheDocument();
  });

  it('shows the revenue trend and top sellers', async () => {
    renderPage();

    expect(await screen.findByRole('img', { name: /Revenue over time/ })).toBeInTheDocument();
    expect(await screen.findByRole('list', { name: 'Top selling products' })).toBeInTheDocument();
    expect(screen.getByText('Pepperoni Pizza')).toBeInTheDocument();
    expect(getProductPerformance).toHaveBeenCalledWith(
      'token-1',
      expect.objectContaining({ sortDirection: 'desc', pageSize: 5 })
    );
  });

  it('says so when there were no sales', async () => {
    vi.mocked(getSalesSummary).mockImplementation(summaryFor(summaryEmpty, summaryEmpty));

    renderPage();

    expect(await screen.findByText('No sales were recorded in this period.')).toBeInTheDocument();
    expect(screen.getAllByText('No previous-period data').length).toBe(4);
  });

  it('shows an error in one section without hiding the others', async () => {
    vi.mocked(getSalesSummary).mockRejectedValue({ status: 500, message: 'Analytics unavailable.' });

    renderPage();

    const kpis = await screen.findByRole('region', { name: 'Key figures' });
    expect(await within(kpis).findByRole('alert')).toHaveTextContent('Analytics unavailable.');
    expect(await screen.findByText('Pepperoni Pizza')).toBeInTheDocument();
  });

  it('refetches when the period changes and on refresh', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Pepperoni Pizza');

    await user.selectOptions(screen.getByLabelText('Period'), 'Last 7 days');

    await waitFor(() => {
      const [, query] = vi.mocked(getSalesOverTime).mock.lastCall;
      expect((new Date(query.to) - new Date(query.from)) / DAY_MS).toBe(7);
    });

    const calls = vi.mocked(getSalesOverTime).mock.calls.length;
    await user.click(screen.getByRole('button', { name: 'Refresh' }));

    await waitFor(() =>
      expect(vi.mocked(getSalesOverTime).mock.calls.length).toBeGreaterThan(calls)
    );
  });
});
