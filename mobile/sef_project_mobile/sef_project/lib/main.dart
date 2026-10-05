import 'package:flutter/material.dart';
import 'package:flutter_stripe/flutter_stripe.dart';
import 'app.dart';
import 'config/stripe_config.dart';
import 'services/customer_api.dart';
import 'state/customer_store.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  if (stripePublishableKey.isNotEmpty) {
    Stripe.publishableKey = stripePublishableKey;
    await Stripe.instance.applySettings();
  }

  final store = CustomerStore(CustomerApi());
  await store.initialize();
  runApp(CustomerShoppingApp(store: store));
}
