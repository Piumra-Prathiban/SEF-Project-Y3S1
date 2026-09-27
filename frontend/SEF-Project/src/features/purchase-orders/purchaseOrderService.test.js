import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiRequest } from '../../services/api';
import { getVariantOptions } from './purchaseOrderService';

vi.mock('../../services/api', () => ({ apiRequest: vi.fn() }));

beforeEach(() => {
  vi.clearAllMocks();
});

describe('getVariantOptions', () => {
  it('includes variants from every product page', async () => {
    apiRequest
      .mockResolvedValueOnce({
        totalPages: 2,
        items: [{
          name: 'Cotton Shirt',
          variants: [{ id: 'first-variant', sku: 'SHIRT-S', name: 'Small' }],
        }],
      })
      .mockResolvedValueOnce({
        items: [{
          name: 'Wool Coat',
          variants: [{ id: 'last-variant', sku: 'COAT-M', name: 'Medium' }],
        }],
      });

    expect(await getVariantOptions('staff-token')).toEqual([
      { id: 'first-variant', sku: 'SHIRT-S', productName: 'Cotton Shirt', variantName: 'Small' },
      { id: 'last-variant', sku: 'COAT-M', productName: 'Wool Coat', variantName: 'Medium' },
    ]);
    expect(apiRequest).toHaveBeenNthCalledWith(1, '/products', {
      method: 'GET',
      token: 'staff-token',
      query: { pageSize: 100, sortBy: 'name', sortDirection: 'asc', page: 1 },
    });
    expect(apiRequest).toHaveBeenNthCalledWith(2, '/products', {
      method: 'GET',
      token: 'staff-token',
      query: { pageSize: 100, sortBy: 'name', sortDirection: 'asc', page: 2 },
    });
  });
});
