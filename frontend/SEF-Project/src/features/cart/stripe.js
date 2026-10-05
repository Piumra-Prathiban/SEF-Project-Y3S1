import { loadStripe } from '@stripe/stripe-js';

const publishableKey = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY;

// Null when no publishable key is configured; checkout then falls back to the
// manual (staff-confirmed) payment flow.
export const stripePromise = publishableKey ? loadStripe(publishableKey) : null;
