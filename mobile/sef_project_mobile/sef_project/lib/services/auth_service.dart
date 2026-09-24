import '../models/auth_models.dart';
import '../services/api_client.dart';

class AuthService {
  AuthService(this._client);

  final ApiClient _client;

  Future<AuthSession> login({required String email, required String password}) async {
    final data = await _client.post(
      '/Auth/login',
      body: {'email': email, 'password': password},
    );

    return AuthSession.fromJson(data as Map<String, dynamic>);
  }
}

class AuthSession {
  const AuthSession({
    required this.token,
    required this.expiresAt,
    required this.user,
  });

  final String token;
  final DateTime expiresAt;
  final UserProfile user;

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
        token: json['token'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String),
        user: UserProfile.fromJson(json['user'] as Map<String, dynamic>),
      );
}
