export function validateSizeForm(size) {
  const errors = [];

  if (!size.name?.trim()) {
    errors.push('Size name is required.');
  }

  if (!size.code?.trim()) {
    errors.push('Size code is required.');
  }

  if (size.name && size.name.length > 100) {
    errors.push('Size name must be 100 characters or fewer.');
  }

  if (size.code && size.code.length > 20) {
    errors.push('Size code must be 20 characters or fewer.');
  }

  return errors;
}

export function buildSizePayload(size) {
  return {
    name: size.name.trim(),
    code: size.code.trim(),
    isActive: size.isActive,
  };
}

export function filterSizes(sizes, filters) {
  const search = filters.search.trim().toLowerCase();

  return sizes.filter((size) => {
    const matchesSearch =
      !search
      || size.name.toLowerCase().includes(search)
      || size.code?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(size.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}
