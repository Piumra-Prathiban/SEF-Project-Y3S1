/// Holds the JWT issued by `POST /api/Auth/login`.
///
/// The default [InMemoryTokenStore] keeps the token only for the app session.
/// A persistent implementation should use the platform keystore (for example
/// a secure-storage plugin), never plain shared preferences.
abstract class TokenStore {
  Future<String?> read();

  Future<void> write(String token);

  Future<void> clear();
}

class InMemoryTokenStore implements TokenStore {
  InMemoryTokenStore([this._token]);

  String? _token;

  @override
  Future<String?> read() async => _token;

  @override
  Future<void> write(String token) async => _token = token;

  @override
  Future<void> clear() async => _token = null;
}
