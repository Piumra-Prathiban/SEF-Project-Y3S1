import { describe, expect, it } from 'vitest';
import { hasAnyRole, ROLES, STAFF_ROLES } from './roles.js';

describe('authorization role utilities', () => {
  it('allows authenticated staff and administrators for staff protected routes', () => {
    expect(hasAnyRole({ role: ROLES.staff }, STAFF_ROLES)).toBe(true);
    expect(hasAnyRole({ role: ROLES.administrator }, STAFF_ROLES)).toBe(true);
  });

  it('rejects unauthorized customer role for staff/admin screens', () => {
    expect(hasAnyRole({ role: ROLES.customer }, STAFF_ROLES)).toBe(false);
  });

  it('allows routes without role restrictions after authentication', () => {
    expect(hasAnyRole({ role: ROLES.customer }, undefined)).toBe(true);
    expect(hasAnyRole({ role: ROLES.customer }, [])).toBe(true);
  });
});
