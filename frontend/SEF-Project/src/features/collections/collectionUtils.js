export function validateCollectionForm(collection) {
  const errors = [];

  if (!collection.name?.trim()) {
    errors.push('Collection name is required.');
  }

  if (collection.name && collection.name.length > 200) {
    errors.push('Collection name must be 200 characters or fewer.');
  }

  if (collection.description && collection.description.length > 1000) {
    errors.push('Description must be 1000 characters or fewer.');
  }

  return errors;
}

export function buildCollectionPayload(collection) {
  return {
    name: collection.name.trim(),
    description: collection.description?.trim() || null,
    isActive: collection.isActive,
  };
}

export function filterCollections(collections, filters) {
  const search = filters.search.trim().toLowerCase();

  return collections.filter((collection) => {
    const matchesSearch =
      !search
      || collection.name.toLowerCase().includes(search)
      || collection.description?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(collection.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}
