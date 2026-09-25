import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/token_store.dart';
import 'api_exception.dart';

/// Thin JSON client for the SEF Project ASP.NET Core API.
///
/// * Attaches the bearer token when one is stored, but refuses to send it
///   over plain HTTP to anything other than a local development host.
/// * Clears the stored token when the API answers 401.
/// * Converts transport and HTTP failures into [ApiException]s.
class ApiClient {
  ApiClient({
    required this._baseUri,
    http.Client? httpClient,
    TokenStore? tokenStore,
    this.timeout = const Duration(seconds: 15),
  })  : _http = httpClient ?? http.Client(),
        _tokenStore = tokenStore ?? InMemoryTokenStore();

  static const Set<String> localHosts = {'localhost', '127.0.0.1', '10.0.2.2'};

  final Uri _baseUri;
  final http.Client _http;
  final TokenStore _tokenStore;
  final Duration timeout;

  Uri buildUri(String path, [Map<String, String?>? query]) {
    final base = _baseUri.toString().replaceFirst(RegExp(r'/+$'), '');
    final uri = Uri.parse('$base$path');
    final params = <String, String>{
      for (final entry in (query ?? const <String, String?>{}).entries)
        if (entry.value != null && entry.value!.isNotEmpty) entry.key: entry.value!,
    };

    return params.isEmpty ? uri : uri.replace(queryParameters: params);
  }

  static bool isSecureOrLocal(Uri uri) =>
      uri.scheme == 'https' || localHosts.contains(uri.host);

  Future<Object?> getJson(String path, {Map<String, String?>? query}) async {
    final uri = buildUri(path, query);
    final headers = <String, String>{'Accept': 'application/json'};

    final token = await _tokenStore.read();
    if (token != null && token.isNotEmpty) {
      if (!isSecureOrLocal(uri)) {
        throw const ApiException(
          kind: ApiErrorKind.insecureConnection,
          message: 'A secure connection is required.',
        );
      }
      headers['Authorization'] = 'Bearer $token';
    }

    final http.Response response;
    try {
      response = await _http.get(uri, headers: headers).timeout(timeout);
    } on TimeoutException {
      throw const ApiException.network('The server took too long to respond.');
    } on http.ClientException {
      throw const ApiException.network(
        'Could not reach the server. Check your connection and try again.',
      );
    }

    if (response.statusCode == 401) {
      await _tokenStore.clear();
    }

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException.fromResponse(response.statusCode, response.body);
    }

    if (response.bodyBytes.isEmpty) {
      return null;
    }

    try {
      return jsonDecode(utf8.decode(response.bodyBytes));
    } on FormatException {
      throw const ApiException(
        kind: ApiErrorKind.invalidResponse,
        message: 'The server sent an unexpected response.',
      );
    }
  }

  void close() => _http.close();
}
