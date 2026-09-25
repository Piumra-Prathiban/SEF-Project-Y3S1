import { describe, expect, it } from 'vitest';
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

    expect(errors).toEqual([
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

    expect(payload).toEqual({
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

    expect(results).toEqual([colours[1]]);
  });

  it('validates three and six character hex values', () => {
    expect(isValidHexCode('#fff')).toBe(true);
    expect(isValidHexCode('#ffffff')).toBe(true);
    expect(isValidHexCode('ffffff')).toBe(false);
  });
});
