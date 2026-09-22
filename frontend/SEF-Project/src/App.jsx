import { useEffect, useState } from 'react';
import { useAuth } from './contexts/AuthContext';

const heroSlides = [
  { 
    eyebrow: 'THE NEW SEASON',
    title: 'Everyday,\nElevated.',
    description:
      'Discover refined essentials and effortless silhouettes designed for the way you live.',
    button: 'Shop New Arrivals',
    image:
      'https://images.unsplash.com/photo-1496747611176-843222e1e57c?auto=format&fit=crop&w=2200&q=90',
  },
  {
    eyebrow: 'CLOTHIC WOMAN',
    title: 'Own Your\nEveryday.',
    description:
      'Modern tailoring, soft textures and timeless pieces made to move with you.',
    button: 'Shop Women', 
    image:
      'https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=2200&q=90',
  },
  {
    eyebrow: 'CLOTHIC MAN',
    title: 'Made For\nYour Pace.',
    description:
      'Clean lines, effortless layers and everyday essentials for modern living.',
    button: 'Shop Men',
    image:
      'https://images.unsplash.com/photo-1610652492500-ded49ceeb378?auto=format&fit=crop&w=2200&q=90',
  },
];

const products = [
  {
    name: 'Relaxed Linen Shirt', 
    category: 'Women · Shirts',
    price: '$49.90',
    image:
      'https://images.unsplash.com/photo-1605763240000-7e93b172d754?auto=format&fit=crop&w=900&q=85',
  },
  {
    name: 'Minimal Knit Sweater',
    category: 'Women · Knitwear',
    price: '$59.90',
    image:
      'https://images.unsplash.com/photo-1434389677669-e08b4cac3105?auto=format&fit=crop&w=900&q=85',
  },
  {
    name: 'Essential Overshirt',
    category: 'Men · Shirts',
    price: '$64.90',
    image:
      'https://images.unsplash.com/photo-1598032895397-b9472444bf93?auto=format&fit=crop&w=900&q=85',
  },
  {
    name: 'Straight Leg Denim',
    category: 'Women · Denim',
    price: '$54.90',
    image:
      'https://images.unsplash.com/photo-1541099649105-f69ad21f3246?auto=format&fit=crop&w=900&q=85',
  },
];

const categories = [
  {
    title: 'Women',
    subtitle: 'Effortless essentials',
    image:
      'https://images.unsplash.com/photo-1485968579580-b6d095142e6e?auto=format&fit=crop&w=1200&q=85',
  },
  {
    title: 'Men',
    subtitle: 'Modern everyday',
    image:
      'https://images.unsplash.com/photo-1516826957135-700dedea698c?auto=format&fit=crop&w=1200&q=85',
  },
  {
    title: 'Accessories',
    subtitle: 'The finishing touch',
    image:
      'https://images.unsplash.com/photo-1492707892479-7bc8d5a4ee93?auto=format&fit=crop&w=1200&q=85',
  },
];

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="11" cy="11" r="7" />
      <path d="M20 20l-4-4" />
    </svg>
  );
}

function UserIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c.8-4 3.5-6 8-6s7.2 2 8 6" />
    </svg>
  );
}

function HeartIcon({ filled = false }) {
  return (
    <svg
      viewBox="0 0 24 24"
      aria-hidden="true"
      className={filled ? 'heart-filled' : ''}
    >
      <path d="M20.8 8.8c0 5.5-8.8 10.4-8.8 10.4S3.2 14.3 3.2 8.8A4.7 4.7 0 0 1 12 6.1a4.7 4.7 0 0 1 8.8 2.7Z" />
    </svg>
  );
}

function BagIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 8h14l-1 13H6L5 8Z" />
      <path d="M9 8a3 3 0 0 1 6 0" />
    </svg>
  );
}

function MenuIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  );
}

function ArrowIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 12h13" />
      <path d="m13 6 6 6-6 6" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 5l14 14M19 5 5 19" />
    </svg>
  );
}

function App() {
  const {
    user,
    isAuthenticated,
    login,
    logout,
  } = useAuth();

  const [activeSlide, setActiveSlide] = useState(0);
  const [mobileMenu, setMobileMenu] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchValue, setSearchValue] = useState('');
  const [accountOpen, setAccountOpen] = useState(false);
  const [favorites, setFavorites] = useState([]);
  const [bagCount, setBagCount] = useState(0);
  const [toast, setToast] = useState('');

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loginError, setLoginError] = useState('');

  useEffect(() => {
    const timer = setInterval(() => {
      setActiveSlide((current) => (current + 1) % heroSlides.length);
    }, 6000);

    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!toast) return;

    const timer = setTimeout(() => setToast(''), 2200);

    return () => clearTimeout(timer);
  }, [toast]);

  function showToast(message) {
    setToast(message);
  }

  function toggleFavorite(index) {
    setFavorites((current) =>
      current.includes(index)
        ? current.filter((item) => item !== index)
        : [...current, index]
    );

    showToast(
      favorites.includes(index)
        ? 'Removed from favorites'
        : 'Added to favorites'
    );
  }

  function addToBag(productName) {
    setBagCount((current) => current + 1);
    showToast(`${productName} added to your bag`);
  }

  async function handleLogin(event) {
    event.preventDefault();
    setLoginError('');

    try {
      await login(email, password);
      setAccountOpen(false);
      setEmail('');
      setPassword('');
      showToast('Welcome back to Clothic');
    } catch (err) {
      setLoginError(err?.message || 'Unable to sign in.');
    }
  }

  function handleNavClick() {
    setMobileMenu(false);
  }

  return (
    <div className="clothing-app">
      {/* Announcement bar */}
      <div className="announcement-bar">
        <span>FREE SHIPPING ON ORDERS OVER $75</span>
        <span className="announcement-divider">•</span>
        <span>NEW SEASON · NEW YOU</span>
      </div>

      {/* Header */}
      <header className="site-header">
        <button
          className="mobile-menu-button"
          onClick={() => setMobileMenu(true)}
          aria-label="Open menu"
        >
          <MenuIcon />
        </button>

        <a href="#top" className="brand">
          CLOTHIC
        </a>

        <nav className="desktop-nav">
          <a href="#women">Women</a>
          <a href="#men">Men</a>
          <a href="#new-in">New In</a>
          <a href="#collections">Collections</a>
          <a href="#sale">Sale</a>
        </nav>

        <div className="header-actions">
          <button
            className="header-icon"
            onClick={() => setSearchOpen(true)}
            aria-label="Search"
          >
            <SearchIcon />
          </button>

          <button
            className="header-icon account-button"
            onClick={() => setAccountOpen(true)}
            aria-label="Account"
          >
            <UserIcon />
          </button>

          <button
            className="header-icon favorite-button"
            onClick={() => showToast(`${favorites.length} saved item${favorites.length === 1 ? '' : 's'}`)}
            aria-label="Favorites"
          >
            <HeartIcon />
            {favorites.length > 0 && (
              <span className="icon-count">{favorites.length}</span>
            )}
          </button>

          <button
            className="header-icon"
            onClick={() => showToast(`${bagCount} item${bagCount === 1 ? '' : 's'} in your bag`)}
            aria-label="Shopping bag"
          >
            <BagIcon />
            {bagCount > 0 && (
              <span className="icon-count">{bagCount}</span>
            )}
          </button>
        </div>
      </header>

      {/* Mobile navigation */}
      <div
        className={`mobile-menu-overlay ${mobileMenu ? 'visible' : ''}`}
        onClick={() => setMobileMenu(false)}
      />

      <aside className={`mobile-menu ${mobileMenu ? 'open' : ''}`}>
        <div className="mobile-menu-header">
          <span className="mobile-menu-title">CLOTHIC</span>

          <button
            className="header-icon"
            onClick={() => setMobileMenu(false)}
            aria-label="Close menu"
          >
            <CloseIcon />
          </button>
        </div>

        <nav className="mobile-nav">
          <a href="#women" onClick={handleNavClick}>
            Women
          </a>
          <a href="#men" onClick={handleNavClick}>
            Men
          </a>
          <a href="#new-in" onClick={handleNavClick}>
            New In
          </a>
          <a href="#collections" onClick={handleNavClick}>
            Collections
          </a>
          <a href="#sale" onClick={handleNavClick}>
            Sale
          </a>
        </nav>

        <div className="mobile-menu-bottom">
          <button onClick={() => {
            setMobileMenu(false);
            setAccountOpen(true);
          }}>
            My Account
          </button>

          <button onClick={() => {
            setMobileMenu(false);
            showToast('Store locator coming soon');
          }}>
            Find a Store
          </button>
        </div>
      </aside>

      {/* Search overlay */}
      <div className={`search-overlay ${searchOpen ? 'open' : ''}`}>
        <div className="search-overlay-top">
          <span className="brand">CLOTHIC</span>

          <button
            className="header-icon"
            onClick={() => setSearchOpen(false)}
            aria-label="Close search"
          >
            <CloseIcon />
          </button>
        </div>

        <div className="search-container">
          <p>WHAT ARE YOU LOOKING FOR?</p>

          <div className="large-search">
            <SearchIcon />

            <input
              autoFocus={searchOpen}
              value={searchValue}
              onChange={(event) => setSearchValue(event.target.value)}
              placeholder="Search clothing, collections, styles..."
            />

            {searchValue && (
              <button onClick={() => setSearchValue('')}>
                <CloseIcon />
              </button>
            )}
          </div>

          <div className="popular-searches">
            <span>Popular searches</span>
            <button onClick={() => setSearchValue('linen')}>Linen</button>
            <button onClick={() => setSearchValue('denim')}>Denim</button>
            <button onClick={() => setSearchValue('shirts')}>Shirts</button>
            <button onClick={() => setSearchValue('dresses')}>Dresses</button>
          </div>
        </div>
      </div>

      {/* Account drawer */}
      <div
        className={`drawer-overlay ${accountOpen ? 'open' : ''}`}
        onClick={() => setAccountOpen(false)}
      />

      <aside className={`account-drawer ${accountOpen ? 'open' : ''}`}>
        <div className="drawer-header">
          <h2>{isAuthenticated ? 'Your Account' : 'Welcome to Clothic'}</h2>

          <button
            className="header-icon"
            onClick={() => setAccountOpen(false)}
          >
            <CloseIcon />
          </button>
        </div>

        {isAuthenticated ? (
          <div className="account-content">
            <div className="account-avatar">
              {user?.email?.charAt(0)?.toUpperCase() || 'C'}
            </div>

            <h3>{user?.email}</h3>
            <p className="account-role">
              {user?.role || 'Clothic customer'}
            </p>

            <div className="account-links">
              <button>My Orders</button>
              <button>Saved Items</button>
              <button>Account Details</button>
            </div>

            <button
              className="dark-button full-button"
              onClick={() => {
                logout();
                setAccountOpen(false);
                showToast('You have been signed out');
              }}
            >
              Sign Out
            </button>
          </div>
        ) : (
          <form className="login-form" onSubmit={handleLogin}>
            <p className="drawer-description">
              Sign in to save your favorites, view your orders and enjoy a
              more personal Clothic experience.
            </p>

            <label>Email address</label>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="you@example.com"
              required
            />

            <label>Password</label>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="••••••••"
              required
            />

            {loginError && (
              <p className="login-error">{loginError}</p>
            )}

            <button className="dark-button full-button" type="submit">
              Sign In
            </button>

            <button
              type="button"
              className="text-button"
              onClick={() => showToast('Registration page coming soon')}
            >
              Create an account
            </button>
          </form>
        )}
      </aside>

      <main id="top">
        {/* Hero */}
        <section className="hero">
          {heroSlides.map((slide, index) => (
            <div
              key={slide.title}
              className={`hero-slide ${
                index === activeSlide ? 'active' : ''
              }`}
              style={{
                backgroundImage: `url("${slide.image}")`,
              }}
            >
              <div className="hero-overlay" />

              <div className="hero-content">
                <span className="hero-eyebrow">{slide.eyebrow}</span>

                <h1>
                  {slide.title.split('\n').map((line) => (
                    <span key={line}>
                      {line}
                      <br />
                    </span>
                  ))}
                </h1>

                <p>{slide.description}</p>

                <button
                  className="hero-button"
                  onClick={() => {
                    document
                      .getElementById('new-in')
                      ?.scrollIntoView({ behavior: 'smooth' });
                  }}
                >
                  {slide.button}
                  <ArrowIcon />
                </button>
              </div>
            </div>
          ))}

          <div className="hero-controls">
            <div className="hero-dots">
              {heroSlides.map((slide, index) => (
                <button
                  key={slide.title}
                  className={index === activeSlide ? 'active' : ''}
                  onClick={() => setActiveSlide(index)}
                  aria-label={`Go to slide ${index + 1}`}
                />
              ))}
            </div>

            <div className="hero-counter">
              <span>0{activeSlide + 1}</span>
              <div />
              <span>0{heroSlides.length}</span>
            </div>
          </div>
        </section>

        {/* Intro */}
        <section className="intro-section">
          <span className="section-kicker">CLOTHIC / 2026</span>

          <h2>
            Style that feels
            <br />
            <em>like you.</em>
          </h2>

          <p>
            Thoughtfully designed clothing for modern lives. Discover pieces
            that move effortlessly from one moment to the next.
          </p>
        </section>

        {/* New arrivals */}
        <section className="products-section" id="new-in">
          <div className="section-heading">
            <div>
              <span className="section-kicker">JUST LANDED</span>
              <h2>New Arrivals</h2>
            </div>

            <button className="outline-link">
              View all
              <ArrowIcon />
            </button>
          </div>

          <div className="product-grid">
            {products.map((product, index) => (
              <article className="product-card" key={product.name}>
                <div className="product-image">
                  <img
                    src={product.image}
                    alt={product.name}
                    loading="lazy"
                  />

                  <button
                    className="product-heart"
                    onClick={() => toggleFavorite(index)}
                    aria-label={`Favorite ${product.name}`}
                  >
                    <HeartIcon filled={favorites.includes(index)} />
                  </button>

                  <button
                    className="quick-add"
                    onClick={() => addToBag(product.name)}
                  >
                    Quick add
                  </button>
                </div>

                <div className="product-info">
                  <div>
                    <h3>{product.name}</h3>
                    <p>{product.category}</p>
                  </div>

                  <strong>{product.price}</strong>
                </div>
              </article>
            ))}
          </div>
        </section>

        {/* Editorial split */}
        <section className="editorial" id="women">
          <div className="editorial-image">
            <img
              src="https://images.unsplash.com/photo-1485230895905-ec40ba36b9bc?auto=format&fit=crop&w=1600&q=90"
              alt="Clothic women's collection"
              loading="lazy"
            />
          </div>

          <div className="editorial-copy">
            <span className="section-kicker">THE WOMAN EDIT</span>

            <h2>
              Soft
              <br />
              structure.
            </h2>

            <p>
              Tailored silhouettes, natural textures and effortless layers
              designed for every version of you.
            </p>

            <button className="dark-button">
              Explore Women
              <ArrowIcon />
            </button>
          </div>
        </section>

        {/* Category cards */}
        <section className="category-section" id="collections">
          <div className="section-heading centered">
            <div>
              <span className="section-kicker">EXPLORE CLOTHIC</span>
              <h2>Find your style</h2>
            </div>
          </div>

          <div className="category-grid">
            {categories.map((category) => (
              <a
                href={category.title === 'Women' ? '#women' : '#men'}
                className="category-card"
                key={category.title}
              >
                <img
                  src={category.image}
                  alt={category.title}
                  loading="lazy"
                />

                <div className="category-overlay" />

                <div className="category-content">
                  <span>{category.subtitle}</span>
                  <h3>{category.title}</h3>
                  <div className="category-link">
                    Shop now
                    <ArrowIcon />
                  </div>
                </div>
              </a>
            ))}
          </div>
        </section>

        {/* Men's editorial */}
        <section className="full-editorial" id="men">
          <img
            src="https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?auto=format&fit=crop&w=2200&q=90"
            alt="Clothic menswear collection"
            loading="lazy"
          />

          <div className="full-editorial-overlay" />

          <div className="full-editorial-content">
            <span className="section-kicker light">
              THE MODERN MAN
            </span>

            <h2>Less effort.<br />More style.</h2>

            <p>
              Everyday layers, refined essentials and pieces built for
              wherever the day takes you.
            </p>

            <button
              className="light-button"
              onClick={() =>
                document
                  .getElementById('men')
                  ?.scrollIntoView({ behavior: 'smooth' })
              }
            >
              Discover Men
              <ArrowIcon />
            </button>
          </div>
        </section>

        {/* Sale / offer */}
        <section className="offer-section" id="sale">
          <div className="offer-content">
            <span className="section-kicker">CLOTHIC MEMBERS</span>

            <h2>
              Your wardrobe.
              <br />
              <em>Your rules.</em>
            </h2>

            <p>
              Sign up for early access to new drops, exclusive edits and
              special offers.
            </p>

            <button
              className="dark-button"
              onClick={() => setAccountOpen(true)}
            >
              Join Clothic
              <ArrowIcon />
            </button>
          </div>

          <div className="offer-decoration">
            <span>CL</span>
            <span>O</span>
            <span>TH</span>
            <span>IC</span>
          </div>
        </section>

        {/* Newsletter */}
        <section className="newsletter">
          <div>
            <span className="section-kicker">STAY IN THE LOOP</span>

            <h2>Be the first to know.</h2>

            <p>
              New arrivals, styling inspiration and exclusive Clothic news,
              delivered occasionally.
            </p>
          </div>

          <form
            className="newsletter-form"
            onSubmit={(event) => {
              event.preventDefault();
              showToast('Thanks for joining Clothic');
              event.currentTarget.reset();
            }}
          >
            <input
              type="email"
              placeholder="Email address"
              required
            />

            <button type="submit">
              Subscribe
              <ArrowIcon />
            </button>
          </form>
        </section>
      </main>

      {/* Footer */}
      <footer className="site-footer">
        <div className="footer-main">
          <div className="footer-brand">
            <a href="#top" className="brand footer-logo">
              CLOTHIC
            </a>

            <p>
              Modern clothing for
              <br />
              everyday living.
            </p>

            <div className="social-links">
              <a href="#instagram">Instagram</a>
              <a href="#tiktok">TikTok</a>
              <a href="#pinterest">Pinterest</a>
            </div>
          </div>

          <div className="footer-column">
            <h4>Shop</h4>
            <a href="#women">Women</a>
            <a href="#men">Men</a>
            <a href="#new-in">New In</a>
            <a href="#collections">Collections</a>
            <a href="#sale">Sale</a>
          </div>

          <div className="footer-column">
            <h4>Help</h4>
            <a href="#customer-service">Customer Service</a>
            <a href="#shipping">Shipping & Returns</a>
            <a href="#size-guide">Size Guide</a>
            <a href="#contact">Contact Us</a>
            <a href="#stores">Find a Store</a>
          </div>

          <div className="footer-column">
            <h4>About</h4>
            <a href="#about">About Clothic</a>
            <a href="#sustainability">Sustainability</a>
            <a href="#careers">Careers</a>
            <a href="#privacy">Privacy</a>
            <a href="#terms">Terms</a>
          </div>
        </div>

        <div className="footer-bottom">
          <span>© 2026 CLOTHIC. ALL RIGHTS RESERVED.</span>
          <span>MADE FOR EVERYDAY.</span>
        </div>
      </footer>

      {/* Toast */}
      <div className={`toast ${toast ? 'show' : ''}`}>
        <span>{toast}</span>
      </div>
    </div>
  );
}

export default App;
