import { afterEach, describe, expect, it } from 'vitest';
import {
  adjustStock,
  approveInventoryWorkflow,
  createCategory,
  createCollection,
  createInventoryWorkflow,
  createProduct,
  createVariant,
  deleteCategory,
  deleteCollection,
  deleteProduct,
  deleteVariant,
  getInventory,
  getLowStock,
  getProducts,
  getProductVariants,
  getStockHistory,
  rejectInventoryWorkflow,
  reviseInventoryWorkflow,
  updateCategory,
  updateCollection,
  updateProduct,
  updateVariant,
} from './catalogApi.js';

function mockFetch(data, ok = true, status = 200) {
  const calls = [];

  globalThis.fetch = async (url, options) => {
    calls.push({ url, options });

    return {
      ok,
      status,
      statusText: ok ? 'OK' : 'Error',
      async json() {
        return data;
      },
    };
  };

  return calls;
}

afterEach(() => {
  delete globalThis.fetch;
});

describe('catalog API integration functions', () => {
  it('sends product query parameters to the backend list endpoint', async () => {
    const calls = mockFetch({ items: [], page: 2 });

    await getProducts(
      {
        search: 'shirt',
        categoryId: 'cat-1',
        page: 2,
        pageSize: 25,
      },
      { token: 'token-1' },
    );

    const requestUrl = new URL(calls[0].url);
    expect(requestUrl.pathname).toBe('/api/products');
    expect(requestUrl.searchParams.get('search')).toBe('shirt');
    expect(requestUrl.searchParams.get('categoryId')).toBe('cat-1');
    expect(requestUrl.searchParams.get('page')).toBe('2');
    expect(calls[0].options.headers.Authorization).toBe('Bearer token-1');
  });

  it('uses JSON bodies for product create/update and delete method for deactivation', async () => {
    let calls = mockFetch({ id: 'product-1' });
    await createProduct({ name: 'T-Shirt' }, { token: 'token-1' });
    expect(calls[0].options.method).toBe('POST');
    expect(calls[0].options.headers['Content-Type']).toBe('application/json');

    calls = mockFetch({ id: 'product-1' });
    await updateProduct('product-1', { name: 'Updated' }, { token: 'token-1' });
    expect(calls[0].url.endsWith('/api/products/product-1')).toBe(true);
    expect(calls[0].options.method).toBe('PUT');

    calls = mockFetch(null);
    await deleteProduct('product-1', { token: 'token-1' });
    expect(calls[0].options.method).toBe('DELETE');
  });

  it('covers category and collection CRUD routes', async () => {
    let calls = mockFetch({ id: 'category-1' });
    await createCategory({ name: 'Tops' });
    expect(calls[0].url.endsWith('/api/categories')).toBe(true);

    calls = mockFetch({ id: 'category-1' });
    await updateCategory('category-1', { name: 'Updated' });
    expect(calls[0].options.method).toBe('PUT');

    calls = mockFetch(null);
    await deleteCategory('category-1');
    expect(calls[0].options.method).toBe('DELETE');

    calls = mockFetch({ id: 'collection-1' });
    await createCollection({ name: 'Summer' });
    expect(calls[0].url.endsWith('/api/collections')).toBe(true);

    calls = mockFetch({ id: 'collection-1' });
    await updateCollection('collection-1', { name: 'Winter' });
    expect(calls[0].options.method).toBe('PUT');

    calls = mockFetch(null);
    await deleteCollection('collection-1');
    expect(calls[0].options.method).toBe('DELETE');
  });

  it('covers variant list/create/update/delete routes and conflict propagation', async () => {
    let calls = mockFetch([]);
    await getProductVariants('product-1');
    expect(calls[0].url.endsWith('/api/products/product-1/variants')).toBe(true);

    calls = mockFetch({ id: 'variant-1' });
    await createVariant('product-1', { sku: 'SKU-1' });
    expect(calls[0].options.method).toBe('POST');

    calls = mockFetch({ id: 'variant-1' });
    await updateVariant('variant-1', { sku: 'SKU-2' });
    expect(calls[0].url.endsWith('/api/variants/variant-1')).toBe(true);

    calls = mockFetch(null);
    await deleteVariant('variant-1');
    expect(calls[0].options.method).toBe('DELETE');

    mockFetch({ title: 'Duplicate SKU' }, false, 409);
    await expect(
      createVariant('product-1', { sku: 'SKU-1' }),
    ).rejects.toMatchObject({ status: 409, isConflict: true });
  });

  it('covers inventory query, adjustment success/failure and low-stock route', async () => {
    let calls = mockFetch({ items: [] });
    await getInventory({ sku: 'TSH', page: 1 });
    expect(new URL(calls[0].url).searchParams.get('sku')).toBe('TSH');

    calls = mockFetch({ quantityOnHand: 12 });
    await adjustStock('variant-1', {
      transactionType: 'StockIn',
      quantity: 5,
      reason: 'Delivery',
    });
    expect(calls[0].url.endsWith('/api/inventory/variant-1/adjust')).toBe(true);
    expect(calls[0].options.method).toBe('POST');

    mockFetch({ detail: 'Insufficient stock' }, false, 400);
    await expect(
      adjustStock('variant-1', {
        transactionType: 'StockOut',
        quantity: 999,
        reason: 'Sale',
      }),
    ).rejects.toMatchObject({ status: 400, isValidationError: true });

    calls = mockFetch({ items: [] });
    await getLowStock({ sortBy: 'quantity', page: 2 });
    expect(new URL(calls[0].url).pathname).toBe('/api/inventory/low-stock');
    expect(new URL(calls[0].url).searchParams.get('sortBy')).toBe('quantity');

    calls = mockFetch([{ transactionType: 'StockIn' }]);
    await getStockHistory('variant-1');
    expect(calls[0].url.endsWith('/api/inventory/variant-1/history')).toBe(true);
    expect(calls[0].options.method).toBe('GET');
  });

  it('covers inventory agent workflow create, approve, reject and revise endpoints', async () => {
    let calls = mockFetch({ workflowId: 'wf-1' });
    await createInventoryWorkflow({ objective: 'Analyze stock' });
    expect(calls[0].url.endsWith('/api/inventory/agent/workflows')).toBe(true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'Approved' });
    await approveInventoryWorkflow('wf-1', { note: 'Approved' });
    expect(
      calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/approve'),
    ).toBe(true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'Rejected' });
    await rejectInventoryWorkflow('wf-1', { reason: 'Unsafe' });
    expect(
      calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/reject'),
    ).toBe(true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'RevisionRequested' });
    await reviseInventoryWorkflow('wf-1', { revisionRequest: 'Try again' });
    expect(
      calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/revise'),
    ).toBe(true);
  });
});
