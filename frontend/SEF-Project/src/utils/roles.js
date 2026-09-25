export const ROLES = Object.freeze({
  customer: 'Customer',
  staff: 'Staff',
  administrator: 'Administrator',
});

export const ROLE = Object.freeze({
  Customer: 'Customer',
  Staff: 'Staff',
  Administrator: 'Administrator',
});

export const STAFF_ROLES = [ROLES.staff, ROLES.administrator];

export function hasAnyRole(user, roles) {
  if (!roles || roles.length === 0) {
    return true;
  }

  return roles.includes(user?.role);
}

export function isStaff(user) {
  return (
    user?.role === ROLE.Staff ||
    user?.role === ROLE.Administrator ||
    user?.role === ROLES.staff ||
    user?.role === ROLES.administrator
  );
}

export function isAdministrator(user) {
  return user?.role === ROLE.Administrator || user?.role === ROLES.administrator;
}

}
