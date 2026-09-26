import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InventoryDashboardPage } from './InventoryDashboardPage';

const api = vi.hoisted(() => ({
  getInventory: vi.fn(), getLowStock: vi.fn(), getCategories: vi.fn(),
  getInventoryItem: vi.fn(), getStockHistory: vi.fn(), adjustStock: vi.fn(),
}));
vi.mock('../../hooks/useCatalogApi', () => ({ useCatalogApi: () => api }));

const records = [
  { productVariantId: 'shirt-variant', productId: 'shirt', categoryId: 'tops', productName: 'Cotton Shirt', sku: 'SHR-1', quantityOnHand: 12, reservedQuantity: 2, availableQuantity: 10, reorderLevel: 5 },
  ...Array.from({ length: 11 }, (_, index) => ({ productVariantId: `jacket-${index}`, productId: `jacket-${index}`, categoryId: 'outerwear', productName: 'Jacket', sku: `JKT-${index}`, quantityOnHand: 8, reorderLevel: 2 })),
];

beforeEach(() => {
  vi.clearAllMocks();
  api.getInventory.mockResolvedValue(records);
  api.getLowStock.mockResolvedValue([]);
  api.getCategories.mockResolvedValue([{ id: 'tops', name: 'Tops' }, { id: 'outerwear', name: 'Outerwear' }]);
  api.getInventoryItem.mockResolvedValue(records[0]);
  api.getStockHistory.mockResolvedValue([]);
  api.adjustStock.mockResolvedValue({ ...records[0], quantityOnHand: 10 });
});

it('filters and paginates the full inventory response without fake API query parameters', async () => {
  const user = userEvent.setup();
  render(<InventoryDashboardPage />);

  expect(await screen.findByText('Page 1 of 2', { exact: false })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Next' }));
  expect(screen.getByText('Page 2 of 2', { exact: false })).toBeInTheDocument();
  await user.type(screen.getByRole('textbox', { name: 'Filter inventory by product' }), 'shirt');
  expect(screen.getByText('SHR-1')).toBeInTheDocument();
  expect(screen.queryByText('JKT-10')).not.toBeInTheDocument();
  expect(api.getInventory).toHaveBeenCalledTimes(1);
});

it('sends the selected stock movement as the backend numeric enum', async () => {
  const user = userEvent.setup();
  render(<InventoryDashboardPage />);

  await screen.findByText('SHR-1');
  await user.click(screen.getAllByRole('button', { name: 'Manage stock' })[0]);
  await user.selectOptions(screen.getByRole('combobox', { name: 'Adjustment type' }), 'StockOut');
  await user.type(screen.getByRole('spinbutton', { name: 'Quantity' }), '2');
  await user.type(screen.getByRole('textbox', { name: 'Reason' }), 'Damaged in storage');
  await user.click(screen.getByRole('button', { name: 'Submit stock adjustment' }));

  await waitFor(() => expect(api.adjustStock).toHaveBeenCalledWith('shirt-variant', { type: 2, quantity: 2, reason: 'Damaged in storage' }));
});
