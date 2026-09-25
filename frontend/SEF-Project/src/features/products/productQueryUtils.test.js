import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildProductQuery,
  defaultProductQuery,
  updatePagedQuery,
} from './productQueryUtils.js';

describe('product query utilities', () => {
  it('resets pagination when product filters change', () => {
    const query = updatePagedQuery(
      { ...defaultProductQuery, page: 3 },
      'categoryId',
      'category-1',
    );

    assert.equal(query.categoryId, 'category-1');
    assert.equal(query.page, 1);
  });

  it('preserves pagination when only the page changes', () => {
    const query = updatePagedQuery(defaultProductQuery, 'page', 2);

    assert.equal(query.page, 2);
  });

  it('passes server-side product query parameters through to the API', () => {
    const query = buildProductQuery({
      ...defaultProductQuery,
      search: 'shirt',
      minPrice: '1000',
      maxPrice: '5000',
      page: 2,
      pageSize: 25,
    });

    assert.deepEqual(query, {
      search: 'shirt',
      categoryId: '',
      collectionId: '',
      isActive: '',
      minPrice: '1000',
      maxPrice: '5000',
      sortBy: 'name',
      sortDirection: 'asc',
      page: 2,
      pageSize: 25,
    });
  });
});
