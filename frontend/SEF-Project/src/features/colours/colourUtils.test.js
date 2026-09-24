import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildColourPayload,
  filterColours,
  isValidHexCode,
  validateColourForm,
} from './colourUtils.js';

describe('colour utilities', () => {
  it('validates required colour name and hex format', () => {
    const errors = validateColourForm({
      name: ' ',
      hexCode: 'blue',
      isActive: true,
    });

    assert.deepEqual(errors, [
      'Colour name is required.',
      'Hex code must be a valid value such as #000000.',
    ]);
  });

  it('builds the backend payload with trimmed optional hex code', () => {
    const payload = buildColourPayload({
      name: ' Navy ',
      hexCode: ' #001f3f ',
      isActive: false,
    });

    assert.deepEqual(payload, {
      name: 'Navy',
      hexCode: '#001f3f',
      isActive: false,
    });
  });

  it('filters colours by search and active status', () => {
    const colours = [
      { name: 'Black', hexCode: '#000000', isActive: true },
      { name: 'White', hexCode: '#ffffff', isActive: false },
    ];

    const results = filterColours(colours, {
      search: 'fff',
      isActive: 'false',
    });

    assert.deepEqual(results, [colours[1]]);
  });

  it('validates three and six character hex values', () => {
    assert.equal(isValidHexCode('#fff'), true);
    assert.equal(isValidHexCode('#ffffff'), true);
    assert.equal(isValidHexCode('ffffff'), false);
  });
});
