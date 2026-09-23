import 'package:flutter/material.dart';
import 'app.dart';
import 'services/customer_api.dart';
import 'state/customer_store.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final store = CustomerStore(CustomerApi());
  await store.initialize();
  runApp(CustomerShoppingApp(store: store));
}
