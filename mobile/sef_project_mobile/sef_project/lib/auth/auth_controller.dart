import 'package:flutter/foundation.dart';

import '../models/auth_models.dart';
import '../services/auth_service.dart';

/// Holds the authenticated session in memory only. No token is persisted, which
/// matches the existing React application's behaviour; secure storage is a
/// separate, later concern.
class AuthController extends ChangeNotifier {
  AuthController(this._authService);

  final AuthService _authService;

  String? _token;
  UserProfile? _user;

  String? get token => _token;
  UserProfile? get user => _user;
  bool get isAuthenticated => _token != null;

  Future<void> login({required String email, required String password}) async {
    final session = await _authService.login(email: email, password: password);

    _token = session.token;
    _user = session.user;

    notifyListeners();
  }

  void logout() {
    _token = null;
    _user = null;

    notifyListeners();
  }
}
