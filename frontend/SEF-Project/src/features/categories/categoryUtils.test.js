import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildCategoryPayload,
  filterCategories,
  validateCategoryForm,
} from './categoryUtils.js';

describe('category utilities', () => {
  it('validates required category name', () => {
    const errors = validateCategoryForm({
      name: ' ',
      description: '',
      isActive: true,
    });

    assert.deepEqual(errors, ['Category name is required.']);
  });

  it('builds the backend payload with trimmed fields', () => {
    const payload = buildCategoryPayload({
      name: '  Pizza  ',
      description: '  Menu items  ',
      isActive: true,
    });

    assert.deepEqual(payload, {
      name: 'Pizza',
      description: 'Menu items',
      isActive: true,
    });
  });

  it('filters categories by search and active status', () => {
    const categories = [
      { name: 'Pizza', description: 'Hot meals', isActive: true },
      { name: 'Desserts', description: 'Sweet items', isActive: false },
    ];

    const result = filterCategories(categories, {
      search: 'sweet',
      isActive: 'false',
    });

    assert.deepEqual(result, [categories[1]]);
  });
});
