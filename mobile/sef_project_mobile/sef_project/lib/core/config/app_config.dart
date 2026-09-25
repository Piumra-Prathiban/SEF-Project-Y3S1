/// App configuration supplied at build time, e.g.
/// `flutter run --dart-define=API_BASE_URL=https://api.example.com/api`.
class AppConfig {
  const AppConfig._();

  /// Defaults to the local API as seen from the Android emulator.
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5193/api',
  );

  static Uri get apiBaseUri => Uri.parse(apiBaseUrl);
}
