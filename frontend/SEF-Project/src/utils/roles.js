export const ROLE = Object.freeze({
  Customer: 'Customer',
  Staff: 'Staff',
  Administrator: 'Administrator',
});

export function isStaff(user) {
  return user?.role === ROLE.Staff || user?.role === ROLE.Administrator;
}

export function isAdministrator(user) {
  return user?.role === ROLE.Administrator;
}
