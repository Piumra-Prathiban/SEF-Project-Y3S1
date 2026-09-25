import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';

const CartContext = createContext(null);
const STORAGE_KEY = 'clothic.cart';

function readStoredCart() {
  if (typeof window === 'undefined') {
    return [];
  }

  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    const parsed = stored ? JSON.parse(stored) : [];

    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

/**
 * Client-side cart. It only holds what the shopper picked (variant id, names,
 * price, quantity) — the order total, stock reservation and payment handling
 * stay server-side when the checkout posts to POST /api/orders.
 */
export function CartProvider({ children }) {
  const [items, setItems] = useState(readStoredCart);

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  }, [items]);

  const addItem = useCallback((item) => {
    setItems((current) => {
      const existing = current.find(
        (entry) => entry.variantId === item.variantId,
      );

      if (existing) {
        return current.map((entry) => (
          entry.variantId === item.variantId
            ? { ...entry, quantity: entry.quantity + item.quantity }
            : entry
        ));
      }

      return [...current, item];
    });
  }, []);

  const updateQuantity = useCallback((variantId, quantity) => {
    setItems((current) => current.map((entry) => (
      entry.variantId === variantId
        ? { ...entry, quantity: Math.max(1, quantity) }
        : entry
    )));
  }, []);

  const removeItem = useCallback((variantId) => {
    setItems((current) => current.filter(
      (entry) => entry.variantId !== variantId,
    ));
  }, []);

  const clearCart = useCallback(() => {
    setItems([]);
  }, []);

  const value = useMemo(() => ({
    items,
    itemCount: items.reduce((total, entry) => total + entry.quantity, 0),
    estimatedSubtotal: items.reduce(
      (total, entry) => total + entry.price * entry.quantity,
      0,
    ),
    addItem,
    updateQuantity,
    removeItem,
    clearCart,
  }), [items, addItem, updateQuantity, removeItem, clearCart]);

  return (
    <CartContext.Provider value={value}>
      {children}
    </CartContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCart() {
  const context = useContext(CartContext);

  if (!context) {
    throw new Error('useCart must be used inside a CartProvider');
  }

  return context;
}
