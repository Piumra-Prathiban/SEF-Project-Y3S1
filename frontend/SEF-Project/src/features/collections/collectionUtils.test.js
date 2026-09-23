import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildCollectionPayload,
  filterCollections,
  validateCollectionForm,
} from './collectionUtils.js';

describe('collection utilities', () => {
  it('validates required collection name', () => {
    const errors = validateCollectionForm({
      name: '',
      description: '',
      isActive: true,
    });

    assert.deepEqual(errors, ['Collection name is required.']);
  });

  it('builds the backend payload with trimmed optional description', () => {
    const payload = buildCollectionPayload({
      name: '  Signature Meals  ',
      description: '  ',
      isActive: false,
    });

    assert.deepEqual(payload, {
      name: 'Signature Meals',
      description: null,
      isActive: false,
    });
  });

  it('filters collections by search and status', () => {
    const collections = [
      { name: 'Classic Menu', description: 'Everyday', isActive: true },
      { name: 'Seasonal', description: 'Limited offer', isActive: false },
    ];

    const result = filterCollections(collections, {
      search: 'limited',
      isActive: 'false',
    });

    assert.deepEqual(result, [collections[1]]);
  });
});
