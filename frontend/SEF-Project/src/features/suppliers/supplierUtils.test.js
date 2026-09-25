import { describe, expect, it } from 'vitest';
import {
  buildSupplierPayload,
  filterSuppliers,
  validateSupplierForm,
} from './supplierUtils.js';

describe('supplier utilities', () => {
  it('validates the required supplier name', () => {
    const errors = validateSupplierForm({
      name: ' ',
      contactName: '',
      email: '',
      phone: '',
      isActive: true,
    });

    expect(errors).toEqual(['Supplier name is required.']);
  });

  it('rejects a malformed email address', () => {
    const errors = validateSupplierForm({
      name: 'Atlas Textiles',
      contactName: '',
      email: 'not-an-email',
      phone: '',
      isActive: true,
    });

    expect(errors).toContain('Enter a valid email address.');
  });

  it('builds the backend payload with trimmed fields and null optionals', () => {
    const payload = buildSupplierPayload({
      name: '  Loom & Thread  ',
      contactName: '  Ayesha Fernando  ',
      email: '',
      phone: '  ',
      isActive: false,
    });

    expect(payload).toEqual({
      name: 'Loom & Thread',
      contactName: 'Ayesha Fernando',
      email: null,
      phone: null,
      isActive: false,
    });
  });

  it('filters suppliers by search and active status', () => {
    const suppliers = [
      {
        name: 'Atlas Textiles',
        contactName: 'Nimal Perera',
        email: 'orders@atlastextiles.lk',
        isActive: true,
      },
      {
        name: 'Nordic Footwear',
        contactName: 'Kamal Silva',
        email: 'sales@nordicfootwear.lk',
        isActive: false,
      },
    ];

    const result = filterSuppliers(suppliers, {
      search: 'nordic',
      isActive: 'false',
    });

    expect(result).toEqual([suppliers[1]]);
  });
});
