import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../contexts/AuthContext';
import { useCart } from '../cart/CartContext';
import { ProductCard } from './ProductCard';
import { getStorefrontProducts } from './storefrontService';
import './storefront.css';

export function StorefrontPage() {
  const { isAuthenticated, user, logout } = useAuth();
  const { itemCount } = useCart();

  const [products, setProducts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeCategory, setActiveCategory] = useState('');

  const loadProducts = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await getStorefrontProducts({ limit: 48 });
      setProducts(response ?? []);
    } catch (err) {
      setError(err?.message || 'The collection could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    // This effect intentionally loads the public catalogue on mount.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadProducts();
  }, [loadProducts]);

  const categories = useMemo(() => {
    const byId = new Map();

    products.forEach((product) => {
      if (product.categoryId && product.categoryName) {
        byId.set(product.categoryId, product.categoryName);
      }
    });

    return [...byId.entries()].map(([id, name]) => ({ id, name }));
  }, [products]);

  const visibleProducts = activeCategory
    ? products.filter((product) => product.categoryId === activeCategory)
    : products;

  return (
    <div className="storefront">
      <header className="storefront__header">
        <Link className="storefront__brand" to="/">
          Clothic
        </Link>

        <nav className="storefront__nav" aria-label="Storefront navigation">
          <a href="#catalogue">Shop</a>

          <Link to="/cart">
            Cart{itemCount > 0 ? ` (${itemCount})` : ''}
          </Link>

          {isAuthenticated ? (
            <>
              <Link to="/orders">My orders</Link>
              <Link to="/products">Dashboard</Link>
              <span className="storefront__user">{user?.email}</span>
              <button onClick={logout} type="button">
                Logout
              </button>
            </>
          ) : (
            <Link className="storefront__sign-in" to="/login">
              Sign in
            </Link>
          )}
        </nav>
      </header>

      <section className="storefront__hero">
        <p className="storefront__eyebrow">New season</p>
        <h1>Everyday essentials, made to last.</h1>
        <p className="storefront__lede">
          Tops, bottoms, outerwear and footwear — browse the collection, then
          sign in to buy and track your orders.
        </p>
        <a className="storefront__cta" href="#catalogue">
          Shop the collection
        </a>
      </section>

      <main className="storefront__catalogue" id="catalogue">
        <div className="storefront__catalogue-head">
          <h2>Shop</h2>

          {categories.length > 1 && (
            <div
              aria-label="Filter by category"
              className="storefront__chips"
              role="group"
            >
              <button
                aria-pressed={activeCategory === ''}
                onClick={() => setActiveCategory('')}
                type="button"
              >
                All
              </button>
              {categories.map((category) => (
                <button
                  aria-pressed={activeCategory === category.id}
                  key={category.id}
                  onClick={() => setActiveCategory(category.id)}
                  type="button"
                >
                  {category.name}
                </button>
              ))}
            </div>
          )}
        </div>

        <ApiErrorAlert message={error} onRetry={loadProducts} />

        {isLoading ? (
          <LoadingState message="Loading the collection..." />
        ) : !error && visibleProducts.length === 0 ? (
          <div className="empty-state">
            No products are available yet — check back soon.
          </div>
        ) : (
          <div className="storefront__grid">
            {visibleProducts.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        )}
      </main>

      <footer className="storefront__footer">
        <p>Clothic · SE3090 group project storefront</p>
      </footer>
    </div>
  );
}

export default StorefrontPage;
