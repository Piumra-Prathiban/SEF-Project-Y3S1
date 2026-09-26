export function validateSizeForm(size) {
  const errors = [];
  const displayOrder = Number(size.displayOrder);

  if (!size.name?.trim()) {
    errors.push('Size name is required.');
  }

  if (size.name && size.name.length > 100) {
    errors.push('Size name must be 100 characters or fewer.');
  }

  if (size.description && size.description.length > 500) {
    errors.push('Description must be 500 characters or fewer.');
  }

  if (size.displayOrder === '' || !Number.isInteger(displayOrder) || displayOrder < 0) {
    errors.push('Display order must be a whole number of zero or more.');
  }

  return errors;
}

export function buildSizePayload(size) {
  return {
    name: size.name.trim(),
    description: size.description?.trim() || null,
    displayOrder: Number(size.displayOrder),
    isActive: size.isActive,
  };
}

export function filterSizes(sizes, filters) {
  const search = filters.search.trim().toLowerCase();

  return sizes.filter((size) => {
    const matchesSearch =
      !search
      || size.name.toLowerCase().includes(search)
      || size.description?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(size.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}
