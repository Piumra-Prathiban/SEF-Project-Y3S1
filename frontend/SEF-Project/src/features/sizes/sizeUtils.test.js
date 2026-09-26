import { describe, expect, it } from 'vitest';
import { buildSizePayload, filterSizes, validateSizeForm } from './sizeUtils.js';

describe('size utilities', () => {
  it('validates size name and display order', () => {
    const errors = validateSizeForm({ name: ' ', description: '', displayOrder: '-1', isActive: true });

    expect(errors).toEqual(['Size name is required.', 'Display order must be a whole number of zero or more.']);
  });

  it('builds the backend payload with trimmed fields', () => {
    const payload = buildSizePayload({
      name: ' Medium ',
      description: ' Standard fit ',
      displayOrder: '3',
      isActive: false,
    });

    expect(payload).toEqual({
      name: 'Medium',
      description: 'Standard fit',
      displayOrder: 3,
      isActive: false,
    });
  });

  it('filters sizes by search and active status', () => {
    const sizes = [
      { name: 'Small', description: 'Petite fit', isActive: true },
      { name: 'Large', description: 'Relaxed fit', isActive: false },
    ];

    const results = filterSizes(sizes, {
      search: 'lar',
      isActive: 'false',
    });

    expect(results).toEqual([sizes[1]]);
  });
});
