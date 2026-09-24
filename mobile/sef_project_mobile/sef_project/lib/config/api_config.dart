/// Base URL of the shared ASP.NET Core API.
///
/// Override per environment, e.g. the Android emulator reaches the host
/// machine through 10.0.2.2:
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5193/api
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5193/api',
);
