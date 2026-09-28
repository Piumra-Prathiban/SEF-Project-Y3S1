import 'package:flutter/foundation.dart';

/// Base URL of the shared ASP.NET Core API.
///
/// Defaults to localhost on the web and to the Android emulator's host alias
/// (10.0.2.2) everywhere else. Override per environment, e.g. a physical
/// device on the same network:
///   flutter run --dart-define=API_BASE_URL=http://YOUR_HOST:5193/api
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: kIsWeb
      ? 'http://localhost:5193/api'
      : 'http://10.0.2.2:5193/api',
);
