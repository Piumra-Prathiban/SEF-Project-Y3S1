import 'package:flutter_secure_storage/flutter_secure_storage.dart';

abstract interface class TokenStorage {
  Future<String?> read();
  Future<void> write(String token);
  Future<void> clear();
}

class SecureTokenStorage implements TokenStorage {
  const SecureTokenStorage([this._storage = const FlutterSecureStorage()]);
  static const _key = 'clothic_access_token';
  final FlutterSecureStorage _storage;
  @override Future<String?> read() => _storage.read(key: _key);
  @override Future<void> write(String token) => _storage.write(key: _key, value: token);
  @override Future<void> clear() => _storage.delete(key: _key);
}
