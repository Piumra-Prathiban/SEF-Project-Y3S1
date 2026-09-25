import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  hasAnyRole,
  ROLES,
  STAFF_ROLES,
} from './roles.js';

describe('authorization role utilities', () => {
  it('allows authenticated staff and administrators for Member 1 protected routes', () => {
    assert.equal(hasAnyRole({ role: ROLES.staff }, STAFF_ROLES), true);
    assert.equal(hasAnyRole({ role: ROLES.administrator }, STAFF_ROLES), true);
  });

  it('rejects unauthorized customer role for staff/admin screens', () => {
    assert.equal(hasAnyRole({ role: ROLES.customer }, STAFF_ROLES), false);
  });

  it('allows routes without role restrictions after authentication', () => {
    assert.equal(hasAnyRole({ role: ROLES.customer }, undefined), true);
    assert.equal(hasAnyRole({ role: ROLES.customer }, []), true);
  });
});
