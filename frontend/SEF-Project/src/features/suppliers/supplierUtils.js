const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateSupplierForm(supplier) {
  const errors = [];

  if (!supplier.name?.trim()) {
    errors.push('Supplier name is required.');
  }

  if (supplier.name && supplier.name.length > 200) {
    errors.push('Supplier name must be 200 characters or fewer.');
  }

  if (supplier.contactName && supplier.contactName.length > 200) {
    errors.push('Contact person must be 200 characters or fewer.');
  }

  if (supplier.email && supplier.email.length > 320) {
    errors.push('Email must be 320 characters or fewer.');
  }

  if (supplier.email?.trim() && !EMAIL_PATTERN.test(supplier.email.trim())) {
    errors.push('Enter a valid email address.');
  }

  if (supplier.phone && supplier.phone.length > 50) {
    errors.push('Phone must be 50 characters or fewer.');
  }

  return errors;
}

export function buildSupplierPayload(supplier) {
  return {
    name: supplier.name.trim(),
    contactName: supplier.contactName?.trim() || null,
    email: supplier.email?.trim() || null,
    phone: supplier.phone?.trim() || null,
    isActive: supplier.isActive,
  };
}

export function filterSuppliers(suppliers, filters) {
  const search = filters.search.trim().toLowerCase();

  return suppliers.filter((supplier) => {
    const matchesSearch =
      !search
      || supplier.name.toLowerCase().includes(search)
      || supplier.contactName?.toLowerCase().includes(search)
      || supplier.email?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(supplier.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}
