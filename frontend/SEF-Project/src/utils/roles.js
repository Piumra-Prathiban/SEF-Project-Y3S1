export const ROLES = Object.freeze({
  customer: 'Customer',
  staff: 'Staff',
  administrator: 'Administrator',
});

export const STAFF_ROLES = [ROLES.staff, ROLES.administrator];

export function hasAnyRole(user, roles) {
  if (!roles || roles.length === 0) {
    return true;
  }

  return roles.includes(user?.role);
}

export function isStaff(user) {
  return hasAnyRole(user, STAFF_ROLES);
}

export function isAdministrator(user) {
  return user?.role === ROLES.administrator;
}
