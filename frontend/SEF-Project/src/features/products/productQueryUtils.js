export const defaultProductQuery = {
  search: '',
  categoryId: '',
  collectionId: '',
  isActive: '',
  minPrice: '',
  maxPrice: '',
  sortBy: 'name',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};

export function updatePagedQuery(currentQuery, field, value) {
  return {
    ...currentQuery,
    [field]: value,
    page: field === 'page' ? value : 1,
  };
}

export function buildProductQuery(query) {
  return {
    search: query.search,
    categoryId: query.categoryId,
    collectionId: query.collectionId,
    isActive: query.isActive,
    minPrice: query.minPrice,
    maxPrice: query.maxPrice,
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
    page: query.page,
    pageSize: query.pageSize,
  };
}
