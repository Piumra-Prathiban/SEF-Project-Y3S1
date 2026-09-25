import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildSizePayload,
  filterSizes,
  validateSizeForm,
} from './sizeUtils.js';

describe('size utilities', () => {
  it('validates required size name and code', () => {
    const errors = validateSizeForm({ name: ' ', code: '', isActive: true });

    assert.deepEqual(errors, [
      'Size name is required.',
      'Size code is required.',
    ]);
  });

  it('builds the backend payload with trimmed fields', () => {
    const payload = buildSizePayload({
      name: ' Medium ',
      code: ' M ',
      isActive: false,
    });

    assert.deepEqual(payload, {
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

    assert.deepEqual(results, [sizes[1]]);
  });
});
