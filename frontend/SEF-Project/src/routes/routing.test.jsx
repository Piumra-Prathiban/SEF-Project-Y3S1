import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import { CartProvider } from '../features/cart/CartContext';
import AppRoutes from './index.jsx';

let mockAuth;

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

function stubFetch() {
  globalThis.fetch = async (url) => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    async json() {
      if (String(url).includes('/storefront/')) {
        return [];
      }

      return {
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalItems: 0,
        totalPages: 0,
      };
    },
  });
}

function renderApp(path) {
  window.history.pushState({}, '', path);

  return render(
    <CartProvider>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </CartProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  stubFetch();

  mockAuth = {
    user: { id: 1, email: 'staff@example.com', role: 'Staff' },
    token: 'test-token',
    expiresAt: null,
    isAuthenticated: true,
    isStaffOrAdmin: true,
    login: vi.fn(),
    logout: vi.fn(),
    fetchCurrentUser: vi.fn(),
  };
});

afterEach(() => {
  delete globalThis.fetch;
});

describe('application routing', () => {
  it('redirects unauthenticated visitors to the login page', async () => {
    mockAuth = {
      ...mockAuth,
      user: null,
      token: null,
      isAuthenticated: false,
      isStaffOrAdmin: false,
    };

    renderApp('/orders');

    expect(
      await screen.findByRole('heading', { name: 'Sign in' }),
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe('/login');
  });

  it('shows the public storefront to a visitor who is not signed in', async () => {
    mockAuth = {
      ...mockAuth,
      user: null,
      token: null,
      isAuthenticated: false,
      isStaffOrAdmin: false,
    };

    renderApp('/');

    expect(
      await screen.findByRole('heading', {
        name: 'Everyday essentials, made to last.',
      }),
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe('/');
  });

  it('lets a visitor open the cart without signing in', async () => {
    mockAuth = {
      ...mockAuth,
      user: null,
      token: null,
      isAuthenticated: false,
      isStaffOrAdmin: false,
    };

    renderApp('/cart');

    expect(
      await screen.findByRole('heading', { name: 'Your cart' }),
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe('/cart');
  });

  it('renders the app shell and navigation for an authenticated user', async () => {
    renderApp('/orders');

    const nav = await screen.findByRole('navigation', {
      name: 'Main navigation',
    });

    expect(
      within(nav).getByRole('link', { name: 'Products' }),
    ).toBeInTheDocument();
    expect(
      within(nav).getByRole('link', { name: 'Orders' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Clothic' }),
    ).toBeInTheDocument();
  });

  it('blocks staff-only pages for customers', async () => {
    mockAuth = {
      ...mockAuth,
      user: { ...mockAuth.user, role: 'Customer' },
      isStaffOrAdmin: false,
    };

    renderApp('/categories');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You do not have permission to access this page.',
    );
  });

  it('disables staff-only navigation entries for customers', async () => {
    mockAuth = {
      ...mockAuth,
      user: { ...mockAuth.user, role: 'Customer' },
      isStaffOrAdmin: false,
    };

    renderApp('/orders');

    const nav = await screen.findByRole('navigation', {
      name: 'Main navigation',
    });

    expect(within(nav).getByText('Categories')).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(
      within(nav).getByRole('link', { name: 'Orders' }),
    ).toBeInTheDocument();
  });

  it('navigates between pages from the sidebar', async () => {
    const user = userEvent.setup();

    renderApp('/orders');

    const nav = await screen.findByRole('navigation', {
      name: 'Main navigation',
    });

    await user.click(
      within(nav).getByRole('link', { name: 'Stock History' }),
    );

    expect(
      await screen.findByRole('heading', { name: 'Stock History' }),
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe('/inventory/history');
  });

  it('shows a not-found page for unknown routes', async () => {
    renderApp('/route-that-does-not-exist');

    expect(
      await screen.findByRole('heading', { name: 'Page not found' }),
    ).toBeInTheDocument();
  });
});
