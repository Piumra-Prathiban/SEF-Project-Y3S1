import { describe, expect, it } from 'vitest';
import { buildSizePayload, filterSizes, validateSizeForm } from './sizeUtils.js';

describe('size utilities', () => {
  it('validates required size name and code', () => {
    const errors = validateSizeForm({ name: ' ', code: '', isActive: true });

    expect(errors).toEqual(['Size name is required.', 'Size code is required.']);
  });

  it('builds the backend payload with trimmed fields', () => {
    const payload = buildSizePayload({
      name: ' Medium ',
      code: ' M ',
      isActive: false,
    });

    expect(payload).toEqual({
      name: 'Medium',
      code: 'M',
      isActive: false,
    });
  });

  it('filters sizes by search and active status', () => {
    const sizes = [
      { name: 'Small', code: 'S', isActive: true },
      { name: 'Large', code: 'L', isActive: false },
    ];

    const results = filterSizes(sizes, {
      search: 'lar',
      isActive: 'false',
    });

    expect(results).toEqual([sizes[1]]);
  });
});
