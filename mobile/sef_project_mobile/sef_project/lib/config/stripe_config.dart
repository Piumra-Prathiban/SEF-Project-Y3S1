/// Stripe publishable (public) key for the payment sandbox.
///
/// This is a test-mode key that is safe to expose in the client bundle. Override
/// per environment with:
///   flutter run --dart-define=STRIPE_PUBLISHABLE_KEY=pk_test_...
const String stripePublishableKey = String.fromEnvironment(
  'STRIPE_PUBLISHABLE_KEY',
  defaultValue:
      'pk_test_51UMtQE5ZY25r8jDqL04V1xAnVIQZITKfdjdW5sHRbzRaLQWhykrmdqxHZyTW75zUcx0nxbl8R5QMgduj8789f4Wd00K1RrCiFg',
);
