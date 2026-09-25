import assert from 'node:assert/strict';
import { afterEach, describe, it } from 'node:test';
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
} from './memberOneApi.js';

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

describe('Member 1 API integration functions', () => {
  it('sends product query parameters to the backend list endpoint', async () => {
    const calls = mockFetch({ items: [], page: 2 });

    await getProducts({
      search: 'shirt',
      categoryId: 'cat-1',
      page: 2,
      pageSize: 25,
    }, { token: 'token-1' });

    const requestUrl = new URL(calls[0].url);
    assert.equal(requestUrl.pathname, '/api/products');
    assert.equal(requestUrl.searchParams.get('search'), 'shirt');
    assert.equal(requestUrl.searchParams.get('categoryId'), 'cat-1');
    assert.equal(requestUrl.searchParams.get('page'), '2');
    assert.equal(calls[0].options.headers.Authorization, 'Bearer token-1');
  });

  it('uses JSON bodies for product create/update and delete method for deactivation', async () => {
    let calls = mockFetch({ id: 'product-1' });
    await createProduct({ name: 'T-Shirt' }, { token: 'token-1' });
    assert.equal(calls[0].options.method, 'POST');
    assert.equal(calls[0].options.headers['Content-Type'], 'application/json');

    calls = mockFetch({ id: 'product-1' });
    await updateProduct('product-1', { name: 'Updated' }, { token: 'token-1' });
    assert.equal(calls[0].url.endsWith('/api/products/product-1'), true);
    assert.equal(calls[0].options.method, 'PUT');

    calls = mockFetch(null);
    await deleteProduct('product-1', { token: 'token-1' });
    assert.equal(calls[0].options.method, 'DELETE');
  });

  it('covers category and collection CRUD routes', async () => {
    let calls = mockFetch({ id: 'category-1' });
    await createCategory({ name: 'Tops' });
    assert.equal(calls[0].url.endsWith('/api/categories'), true);

    calls = mockFetch({ id: 'category-1' });
    await updateCategory('category-1', { name: 'Updated' });
    assert.equal(calls[0].options.method, 'PUT');

    calls = mockFetch(null);
    await deleteCategory('category-1');
    assert.equal(calls[0].options.method, 'DELETE');

    calls = mockFetch({ id: 'collection-1' });
    await createCollection({ name: 'Summer' });
    assert.equal(calls[0].url.endsWith('/api/collections'), true);

    calls = mockFetch({ id: 'collection-1' });
    await updateCollection('collection-1', { name: 'Winter' });
    assert.equal(calls[0].options.method, 'PUT');

    calls = mockFetch(null);
    await deleteCollection('collection-1');
    assert.equal(calls[0].options.method, 'DELETE');
  });

  it('covers variant list/create/update/delete routes and conflict propagation', async () => {
    let calls = mockFetch([]);
    await getProductVariants('product-1');
    assert.equal(calls[0].url.endsWith('/api/products/product-1/variants'), true);

    calls = mockFetch({ id: 'variant-1' });
    await createVariant('product-1', { sku: 'SKU-1' });
    assert.equal(calls[0].options.method, 'POST');

    calls = mockFetch({ id: 'variant-1' });
    await updateVariant('variant-1', { sku: 'SKU-2' });
    assert.equal(calls[0].url.endsWith('/api/variants/variant-1'), true);

    calls = mockFetch(null);
    await deleteVariant('variant-1');
    assert.equal(calls[0].options.method, 'DELETE');

    mockFetch({ title: 'Duplicate SKU' }, false, 409);
    await assert.rejects(
      () => createVariant('product-1', { sku: 'SKU-1' }),
      { status: 409, isConflict: true },
    );
  });

  it('covers inventory query, adjustment success/failure and low-stock route', async () => {
    let calls = mockFetch({ items: [] });
    await getInventory({ sku: 'TSH', page: 1 });
    assert.equal(new URL(calls[0].url).searchParams.get('sku'), 'TSH');

    calls = mockFetch({ quantityOnHand: 12 });
    await adjustStock('variant-1', {
      transactionType: 'StockIn',
      quantity: 5,
      reason: 'Delivery',
    });
    assert.equal(calls[0].url.endsWith('/api/inventory/variant-1/adjust'), true);
    assert.equal(calls[0].options.method, 'POST');

    mockFetch({ detail: 'Insufficient stock' }, false, 400);
    await assert.rejects(
      () => adjustStock('variant-1', {
        transactionType: 'StockOut',
        quantity: 999,
        reason: 'Sale',
      }),
      { status: 400, isValidationError: true },
    );

    calls = mockFetch({ items: [] });
    await getLowStock({ sortBy: 'quantity', page: 2 });
    assert.equal(new URL(calls[0].url).pathname, '/api/inventory/low-stock');
    assert.equal(new URL(calls[0].url).searchParams.get('sortBy'), 'quantity');

    calls = mockFetch([{ transactionType: 'StockIn' }]);
    await getStockHistory('variant-1');
    assert.equal(calls[0].url.endsWith('/api/inventory/variant-1/history'), true);
    assert.equal(calls[0].options.method, 'GET');
  });

  it('covers inventory agent workflow create, approve, reject and revise endpoints', async () => {
    let calls = mockFetch({ workflowId: 'wf-1' });
    await createInventoryWorkflow({ objective: 'Analyze stock' });
    assert.equal(calls[0].url.endsWith('/api/inventory/agent/workflows'), true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'Approved' });
    await approveInventoryWorkflow('wf-1', { note: 'Approved' });
    assert.equal(calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/approve'), true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'Rejected' });
    await rejectInventoryWorkflow('wf-1', { reason: 'Unsafe' });
    assert.equal(calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/reject'), true);

    calls = mockFetch({ workflowId: 'wf-1', status: 'RevisionRequested' });
    await reviseInventoryWorkflow('wf-1', { revisionRequest: 'Try again' });
    assert.equal(calls[0].url.endsWith('/api/inventory/agent/workflows/wf-1/revise'), true);
  });
});
