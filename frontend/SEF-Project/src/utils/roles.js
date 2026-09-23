export const ROLES = {
  customer: 'Customer',
  staff: 'Staff',
  administrator: 'Administrator',
};

export const STAFF_ROLES = [ROLES.staff, ROLES.administrator];

export function hasAnyRole(user, roles) {
  if (!roles || roles.length === 0) {
    return true;
  }

  return roles.includes(user?.role);
}
