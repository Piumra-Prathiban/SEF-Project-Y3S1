# Clothic Web

React 19 + Vite front end for **Clothic**, the AI-powered fashion commerce and
retail platform. It serves two audiences from one app:

- the **public storefront** — anonymous visitors browse the collection, open a
  product, pick a size/colour and fill a cart;
- the **signed-in dashboard** — customers manage orders, wishlist and profile,
  while staff and administrators manage catalog, inventory, orders and the
  inventory agent.

## Scripts

| Command | Purpose |
| --- | --- |
| `npm install` | Install dependencies (unset `NODE_ENV` first if it is set to `production`) |
| `npm run dev` | Vite dev server, http://localhost:5173 |
| `npm test` | Vitest + Testing Library (the script sets `NODE_ENV=test`) |
| `npm run lint` | ESLint |
| `npm run build` | Production build into `dist/` |
| `npm run preview` | Serve the production build |

## How it talks to the API

Every request goes through `src/services/api.js`, which attaches the bearer
token and reads `VITE_API_BASE_URL` (default `http://localhost:5193/api`).
Anonymous storefront calls use the public `/api/storefront/*` endpoints; all
staff endpoints stay behind authentication. Business rules, pricing, stock
reservation and the order/payment/shipment state machines live in the API, never
in the browser.

## Structure

```
src/
  components/ui/     shared UI family (PageShell, LoadingState, ApiErrorAlert, Alert)
  components/layout/ app shell + navigation
  contexts/          AuthContext (session, login/logout)
  features/          feature-first modules: storefront, cart, wishlist, profile,
                     orders, products, categories, collections, sizes, colours,
                     variants, inventory, inventory-agent
  routes/            single react-router configuration
  services/          API clients (api, authService, catalogApi, orderService)
  utils/             roles, formatting, api error helpers
```

Anonymous routes are `/` (storefront), `/shop/:id` (product detail), `/cart` and
`/login`; everything else — including `/checkout`, `/orders`, `/wishlist` and
`/profile` — sits behind the protected layout, so placing an order or saving
anything needs an account.

## Tests

Tests live next to the modules they cover (`*.test.jsx` / `*.test.js`) and share
the setup in `src/test/setup.js` (jsdom, jest-dom, `localStorage` reset).
