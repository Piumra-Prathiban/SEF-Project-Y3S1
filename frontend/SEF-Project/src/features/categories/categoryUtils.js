export function validateCategoryForm(category) {
  const errors = [];

  if (!category.name?.trim()) {
    errors.push('Category name is required.');
  }

  if (category.name && category.name.length > 200) {
    errors.push('Category name must be 200 characters or fewer.');
  }

  if (category.description && category.description.length > 1000) {
    errors.push('Description must be 1000 characters or fewer.');
  }

  return errors;
}

export function buildCategoryPayload(category) {
  return {
    name: category.name.trim(),
    description: category.description?.trim() || null,
    isActive: category.isActive,
  };
}

export function filterCategories(categories, filters) {
  const search = filters.search.trim().toLowerCase();

  return categories.filter((category) => {
    const matchesSearch =
      !search
      || category.name.toLowerCase().includes(search)
      || category.description?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(category.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}
