import { describe, expect, it } from 'vitest';
import { resolveImageUrl } from './images';

describe('resolveImageUrl', () => {
  it('returns null for empty values', () => {
    expect(resolveImageUrl(null)).toBeNull();
    expect(resolveImageUrl(undefined)).toBeNull();
    expect(resolveImageUrl('')).toBeNull();
  });

  it('leaves absolute URLs unchanged', () => {
    expect(resolveImageUrl('https://cdn.example.com/a.svg')).toBe(
      'https://cdn.example.com/a.svg',
    );
    expect(resolveImageUrl('http://localhost:5193/images/products/a.svg')).toBe(
      'http://localhost:5193/images/products/a.svg',
    );
  });

  it('leaves data URLs unchanged', () => {
    const dataUrl = 'data:image/svg+xml;base64,PHN2Zy8+';

    expect(resolveImageUrl(dataUrl)).toBe(dataUrl);
  });

  it('prefixes a root-relative path with the API origin', () => {
    expect(resolveImageUrl('/images/products/classic-cotton-tshirt.svg')).toBe(
      'http://localhost:5193/images/products/classic-cotton-tshirt.svg',
    );
  });

  it('prefixes a relative path without a leading slash', () => {
    expect(resolveImageUrl('images/products/slim-fit-denim-jeans.svg')).toBe(
      'http://localhost:5193/images/products/slim-fit-denim-jeans.svg',
    );
  });
});
