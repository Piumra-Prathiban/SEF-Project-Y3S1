import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/api_config.dart';

/// Mirrors the React app's `apiRequest` error shape: the HTTP status plus the
/// backend's ProblemDetails message (detail, falling back to title).
class ApiException implements Exception {
  ApiException(this.status, this.message);

  final int status;
  final String message;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({http.Client? httpClient, this.baseUrl = apiBaseUrl})
      : _httpClient = httpClient ?? http.Client();

  final http.Client _httpClient;
  final String baseUrl;

  Future<dynamic> get(String path, {String? token}) {
    return _send(() => _httpClient.get(_uri(path), headers: _headers(token)));
  }

  Future<dynamic> post(String path, {String? token, Object? body}) {
    return _send(
      () => _httpClient.post(
        _uri(path),
        headers: _headers(token),
        body: body == null ? null : jsonEncode(body),
      ),
    );
  }

  Uri _uri(String path) => Uri.parse('$baseUrl$path');

  Map<String, String> _headers(String? token) => {
        'Content-Type': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      };

  Future<dynamic> _send(Future<http.Response> Function() request) async {
    final http.Response response;

    try {
      response = await request();
    } on Exception {
      throw ApiException(
        0,
        'Could not reach the server. Check your connection and try again.',
      );
    }

    final data = _decode(response.body);

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(response.statusCode, _messageFrom(data));
    }

    return data;
  }

  dynamic _decode(String body) {
    if (body.isEmpty) {
      return null;
    }

    try {
      return jsonDecode(body);
    } on FormatException {
      return null;
    }
  }

  String _messageFrom(dynamic data) {
    if (data is Map<String, dynamic>) {
      final detail = data['detail'];

      if (detail is String && detail.isNotEmpty) {
        return detail;
      }

      final title = data['title'];

      if (title is String && title.isNotEmpty) {
        return title;
      }
    }

    return 'The request failed.';
  }
}
