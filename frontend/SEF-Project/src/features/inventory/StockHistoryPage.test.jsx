import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { StockHistoryPage } from './StockHistoryPage';

const api = {
  getInventory: vi.fn().mockResolvedValue([{ productVariantId: 'variant-1', productName: 'Cotton Shirt', sku: 'SHIRT-M' }]),
  getStockHistory: vi.fn().mockResolvedValue([{ id: 'movement-1', type: 1, quantityChange: 5, quantityOnHandAfter: 12, reason: 'Supplier delivery', performedByUserEmail: 'staff@example.com', createdAt: '2026-09-26T09:00:00Z' }]),
};
vi.mock('../../hooks/useCatalogApi', () => ({ useCatalogApi: () => api }));
vi.mock('../../hooks/useSessionGuard', () => {
  const guard = () => false;
  return { useSessionGuard: () => guard };
});

describe('StockHistoryPage', () => {
  it('shows the selected variant and its recorded stock movements', async () => {
    render(<MemoryRouter initialEntries={['/inventory/history?variantId=variant-1']}><StockHistoryPage /></MemoryRouter>);
    expect(await screen.findByText('Supplier delivery')).toBeInTheDocument();
    expect(api.getStockHistory).toHaveBeenCalledWith('variant-1');
    expect(screen.getByText('Stock in')).toBeInTheDocument();
    expect(screen.getByText('+5')).toBeInTheDocument();
    expect(screen.getByText('staff@example.com')).toBeInTheDocument();
  });
});
