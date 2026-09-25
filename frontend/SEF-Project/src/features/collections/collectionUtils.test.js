import { describe, expect, it } from 'vitest';
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

    expect(errors).toEqual(['Collection name is required.']);
  });

  it('builds the backend payload with trimmed optional description', () => {
    const payload = buildCollectionPayload({
      name: '  Summer Essentials  ',
      description: '  ',
      isActive: false,
    });

    expect(payload).toEqual({
      name: 'Summer Essentials',
      description: null,
      isActive: false,
    });
  });

  it('filters collections by search and status', () => {
    const collections = [
      { name: 'Summer Essentials', description: 'Everyday pieces', isActive: true },
      { name: 'Seasonal Capsule', description: 'Limited offer', isActive: false },
    ];

    const result = filterCollections(collections, {
      search: 'limited',
      isActive: 'false',
    });

    expect(result).toEqual([collections[1]]);
  });
});
