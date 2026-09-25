import { describe, expect, it } from 'vitest';
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

    expect(errors).toEqual(['Category name is required.']);
  });

  it('builds the backend payload with trimmed fields', () => {
    const payload = buildCategoryPayload({
      name: '  Tops  ',
      description: '  Everyday layering  ',
      isActive: true,
    });

    expect(payload).toEqual({
      name: 'Tops',
      description: 'Everyday layering',
      isActive: true,
    });
  });

  it('filters categories by search and active status', () => {
    const categories = [
      { name: 'Tops', description: 'Everyday layering', isActive: true },
      { name: 'Footwear', description: 'Boots and sneakers', isActive: false },
    ];

    const result = filterCategories(categories, {
      search: 'sneakers',
      isActive: 'false',
    });

    expect(result).toEqual([categories[1]]);
  });
});
