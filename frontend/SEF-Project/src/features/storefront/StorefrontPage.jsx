import { useCallback, useEffect, useState } from 'react';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProductCard } from './ProductCard';
import { StorefrontChrome } from './StorefrontChrome';
import { getStorefrontProducts } from './storefrontService';
import './storefront.css';

export function StorefrontPage() {
  const [products, setProducts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeCategory, setActiveCategory] = useState('');
  const [filterOptions, setFilterOptions] = useState({ categories: [], sizes: [], colours: [] });
  const [draftFilters, setDraftFilters] = useState({ search: '', size: '', colour: '', minPrice: '', maxPrice: '', sortBy: 'name', sortDirection: 'asc' });
  const [appliedFilters, setAppliedFilters] = useState({});

  const loadProducts = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await getStorefrontProducts({ limit: 48, ...appliedFilters });
      setProducts(response ?? []);
      setFilterOptions((current) => {
        const categories = new Map(current.categories.map((category) => [category.id, category.name]));
        const sizes = new Set(current.sizes);
        const colours = new Set(current.colours);
        (response ?? []).forEach((product) => {
          if (product.categoryId && product.categoryName) categories.set(product.categoryId, product.categoryName);
          (product.sizes ?? []).forEach((size) => sizes.add(size));
          (product.colours ?? []).forEach((colour) => colours.add(colour.name));
        });
        return {
          categories: [...categories].map(([id, name]) => ({ id, name })),
          sizes: [...sizes].sort(),
          colours: [...colours].sort(),
        };
      });
    } catch (err) {
      setError(err?.message || 'The collection could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, [appliedFilters]);

  useEffect(() => {
    // This effect intentionally loads the public catalogue on mount.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadProducts();
  }, [loadProducts]);

  const visibleProducts = activeCategory
    ? products.filter((product) => product.categoryId === activeCategory)
    : products;

  const { categories, sizes, colours } = filterOptions;
  const hasFilters = Boolean(activeCategory || appliedFilters.search || appliedFilters.size || appliedFilters.colour || appliedFilters.minPrice || appliedFilters.maxPrice);

  function updateDraft(field, value) {
    setDraftFilters((current) => ({ ...current, [field]: value }));
  }

  function applyFilters(event) {
    event.preventDefault();
    setAppliedFilters({
      categoryId: activeCategory,
      search: draftFilters.search.trim(),
      size: draftFilters.size,
      colour: draftFilters.colour,
      minPrice: draftFilters.minPrice,
      maxPrice: draftFilters.maxPrice,
      sortBy: draftFilters.sortBy,
      sortDirection: draftFilters.sortDirection,
    });
  }

  function clearFilters() {
    setActiveCategory('');
    setDraftFilters({ search: '', size: '', colour: '', minPrice: '', maxPrice: '', sortBy: 'name', sortDirection: 'asc' });
    setAppliedFilters({});
  }

  function selectCategory(categoryId) {
    setActiveCategory(categoryId);
    setAppliedFilters((current) => ({ ...current, categoryId }));
  }

  return (
    <StorefrontChrome>
      <section className="storefront__hero">
        <div className="storefront__hero-copy">
          <p className="storefront__eyebrow">A NEW POINT OF VIEW · SEASON 01</p>
          <h1>Everyday essentials, made to last.</h1>
          <p className="storefront__lede">A considered wardrobe for the everyday. Explore thoughtfully selected pieces designed to move with you.</p>
          <a className="storefront__cta" href="#catalogue">Explore the collection <span aria-hidden="true">↗</span></a>
          <div className="storefront__hero-detail"><span>01 / 04</span><span>THE EVERYDAY EDIT</span></div>
        </div>
        <div className="storefront__hero-art" aria-hidden="true"><p>THE ART OF<br />GETTING DRESSED</p></div>
      </section>

      <div className="storefront__trust-strip"><span>Made for everyday life</span><span>Thoughtfully chosen pieces</span><span>Find your perfect fit</span></div>

      <main className="storefront__catalogue" id="catalogue">
        <div className="storefront__catalogue-head">
          <div><p className="storefront__eyebrow">THE COLLECTION</p><h2>Find your new favourite.</h2><p>Good things, made to be worn again and again.</p></div>
        </div>

        <div className="storefront__shop-layout">
          <aside className="storefront__filters" aria-label="Product filters">
            <div className="storefront__filters-title"><h3>Filters</h3>{hasFilters && <button onClick={clearFilters} type="button">Clear all</button>}</div>
            <form onSubmit={applyFilters}>
              <label>Search<input onChange={(event) => updateDraft('search', event.target.value)} placeholder="What are you looking for?" type="search" value={draftFilters.search} /></label>
              <label>Size<select onChange={(event) => updateDraft('size', event.target.value)} value={draftFilters.size}><option value="">All sizes</option>{sizes.map((size) => <option key={size} value={size}>{size}</option>)}</select></label>
              <label>Colour<select onChange={(event) => updateDraft('colour', event.target.value)} value={draftFilters.colour}><option value="">All colours</option>{colours.map((colour) => <option key={colour} value={colour}>{colour}</option>)}</select></label>
              <div className="storefront__price-fields"><label>Min price<input min="0" onChange={(event) => updateDraft('minPrice', event.target.value)} placeholder="0" type="number" value={draftFilters.minPrice} /></label><label>Max price<input min="0" onChange={(event) => updateDraft('maxPrice', event.target.value)} placeholder="Any" type="number" value={draftFilters.maxPrice} /></label></div>
              <button className="storefront__filter-submit" type="submit">Apply filters <span aria-hidden="true">→</span></button>
            </form>
          </aside>

          <div className="storefront__results">
          <div className="storefront__results-top"><span>{isLoading ? 'Finding pieces…' : `${visibleProducts.length} ${visibleProducts.length === 1 ? 'piece' : 'pieces'}`}</span><label>Sort by<select aria-label="Sort products" onChange={(event) => { const [sortBy, sortDirection] = event.target.value.split(':'); updateDraft('sortBy', sortBy); updateDraft('sortDirection', sortDirection); setAppliedFilters((current) => ({ ...current, sortBy, sortDirection })); }} value={`${draftFilters.sortBy}:${draftFilters.sortDirection}`}><option value="name:asc">Name A–Z</option><option value="name:desc">Name Z–A</option><option value="price:asc">Price: low to high</option><option value="price:desc">Price: high to low</option></select></label></div>

          {categories.length > 1 && (
            <div
              aria-label="Filter by category"
              className="storefront__chips"
              role="group"
            >
              <button
                aria-pressed={activeCategory === ''}
                onClick={() => selectCategory('')}
                type="button"
              >
                All
              </button>
              {categories.map((category) => (
                <button
                  aria-pressed={activeCategory === category.id}
                  key={category.id}
                  onClick={() => selectCategory(category.id)}
                  type="button"
                >
                  {category.name}
                </button>
              ))}
            </div>
          )}

        <ApiErrorAlert message={error} onRetry={loadProducts} />

        {isLoading ? (
          <LoadingState message="Loading the collection..." />
        ) : !error && visibleProducts.length === 0 ? (
          <div className="empty-state">
            {hasFilters ? 'No pieces match your filters. Try a different search or clear the filters.' : 'No products are available yet — check back soon.'}
          </div>
        ) : (
          <div className="storefront__grid">
            {visibleProducts.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        )}
          </div>
        </div>
      </main>
    </StorefrontChrome>
  );
}

export default StorefrontPage;
