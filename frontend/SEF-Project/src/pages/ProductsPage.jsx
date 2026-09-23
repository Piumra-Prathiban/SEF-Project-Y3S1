import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState';
import Pagination from '../components/Pagination';
import ProductCard from '../components/ProductCard';
import { getApiErrorMessage } from '../services/api';
import { addCartItem, addWishlistItem, searchProducts } from '../services/shoppingService';

const defaultFilters = { search: '', categoryId: '', minPrice: '', maxPrice: '', inStockOnly: false, sort: 'name:asc' };

export default function ProductsPage() {
  const { isAuthenticated, token } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryString = searchParams.toString();
  const [filters, setFilters] = useState(() => ({
    ...defaultFilters,
    search: searchParams.get('search') || '',
    categoryId: searchParams.get('categoryId') || '',
    minPrice: searchParams.get('minPrice') || '',
    maxPrice: searchParams.get('maxPrice') || '',
    inStockOnly: searchParams.get('inStockOnly') === 'true',
    sort: searchParams.get('sort') || 'name:asc',
  }));
  const [result, setResult] = useState(null);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [validationError, setValidationError] = useState('');
  const [notice, setNotice] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    const params = new URLSearchParams(queryString);
    const [sortBy = 'name', sortDirection = 'asc'] = (params.get('sort') || 'name:asc').split(':');
    Promise.resolve().then(() => {
      if (active) {
        setLoading(true);
        setError('');
      }
      return searchProducts({
      search: params.get('search') || '',
      categoryId: params.get('categoryId') || '',
      minPrice: params.get('minPrice') || '',
      maxPrice: params.get('maxPrice') || '',
      inStockOnly: params.get('inStockOnly') || '',
      sortBy,
      sortDirection,
      page: params.get('page') || 1,
        pageSize: 12,
      });
    }).then((response) => {
      if (!active) return;
      setResult(response);
      setCategories((current) => {
        const known = new Map(current.map((category) => [category.id, category]));
        response.items.forEach((product) => product.categories.forEach((category) => known.set(category.id, category)));
        return [...known.values()].sort((a, b) => a.name.localeCompare(b.name));
      });
    }).catch((requestError) => active && setError(getApiErrorMessage(requestError)))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [queryString, reloadKey]);

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }));
  }

  function submitFilters(event) {
    event.preventDefault();
    const min = filters.minPrice === '' ? null : Number(filters.minPrice);
    const max = filters.maxPrice === '' ? null : Number(filters.maxPrice);
    if ((min !== null && min < 0) || (max !== null && max < 0)) return setValidationError('Prices cannot be negative.');
    if (min !== null && max !== null && min > max) return setValidationError('Minimum price cannot exceed maximum price.');
    setValidationError('');
    const next = {};
    Object.entries(filters).forEach(([key, value]) => { if (value !== '' && value !== false) next[key] = String(value); });
    next.page = '1';
    setSearchParams(next);
  }

  function changePage(page) {
    const next = new URLSearchParams(searchParams);
    next.set('page', page);
    setSearchParams(next);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  function requireLogin() {
    if (isAuthenticated) return true;
    navigate('/login', { state: { from: { pathname: '/products' } } });
    return false;
  }

  async function saveProduct(productId) {
    if (!requireLogin()) return;
    try { await addWishlistItem(token, productId); setNotice('Saved to your wishlist.'); }
    catch (requestError) { setNotice(getApiErrorMessage(requestError)); }
  }

  async function addProductToCart(variantId, quantity) {
    if (!requireLogin()) return;
    try { await addCartItem(token, variantId, quantity); setNotice('Added to your cart.'); }
    catch (requestError) { setNotice(getApiErrorMessage(requestError)); }
  }

  return (
    <section>
      <div className="page-hero"><div><p className="kicker">Advanced discovery</p><h1>Find your next favourite</h1><p>Search active products, compare variants, and check live availability.</p></div></div>
      <form className="filter-panel" onSubmit={submitFilters}>
        <label className="filter-search">Search products<input type="search" placeholder="Name, description, variant or SKU" value={filters.search} maxLength="100" onChange={(event) => updateFilter('search', event.target.value)} /></label>
        <label>Category<select value={filters.categoryId} onChange={(event) => updateFilter('categoryId', event.target.value)}><option value="">All categories</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        <label>Minimum price<input type="number" min="0" value={filters.minPrice} onChange={(event) => updateFilter('minPrice', event.target.value)} /></label>
        <label>Maximum price<input type="number" min="0" value={filters.maxPrice} onChange={(event) => updateFilter('maxPrice', event.target.value)} /></label>
        <label>Sort by<select value={filters.sort} onChange={(event) => updateFilter('sort', event.target.value)}><option value="name:asc">Name A–Z</option><option value="name:desc">Name Z–A</option><option value="price:asc">Price low to high</option><option value="price:desc">Price high to low</option><option value="newest:desc">Newest first</option></select></label>
        <label className="checkbox-field"><input type="checkbox" checked={filters.inStockOnly} onChange={(event) => updateFilter('inStockOnly', event.target.checked)} />In-stock products only</label>
        <button type="submit" className="button button--primary">Apply filters</button>
        <button type="button" className="button button--ghost" onClick={() => { setFilters(defaultFilters); setSearchParams({ page: '1' }); }}>Reset</button>
        {validationError && <p className="form-error filter-error" role="alert">{validationError}</p>}
      </form>
      {notice && <div className="notice" role="status"><span>{notice}</span><button type="button" className="text-button" onClick={() => setNotice('')}>Dismiss</button></div>}
      {loading && <LoadingState message="Finding products…" />}
      {!loading && error && <ErrorState message={error} onRetry={() => setReloadKey((key) => key + 1)} />}
      {!loading && !error && result?.items.length === 0 && <EmptyState title="No products found" message="Try a broader search or remove one of your filters." />}
      {!loading && !error && result?.items.length > 0 && <><div className="results-heading"><p><strong>{result.totalCount}</strong> products found</p><p>Page {result.page} of {result.totalPages}</p></div><div className="product-grid">{result.items.map((product) => <ProductCard key={product.id} product={product} onSave={saveProduct} onAddToCart={addProductToCart} />)}</div><Pagination page={result.page} totalPages={result.totalPages} onPageChange={changePage} /></>}
    </section>
  );
}
