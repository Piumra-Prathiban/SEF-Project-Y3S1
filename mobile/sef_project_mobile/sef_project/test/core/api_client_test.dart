import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sef_project/core/api/api_client.dart';
import 'package:sef_project/core/api/api_exception.dart';
import 'package:sef_project/core/auth/token_store.dart';

http.Response jsonResponse(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

ApiClient clientFor(
  MockClientHandler handler, {
  String baseUrl = 'https://api.example.com/api',
  TokenStore? tokens,
  Duration timeout = const Duration(seconds: 5),
}) =>
    ApiClient(
      baseUri: Uri.parse(baseUrl),
      httpClient: MockClient(handler),
      tokenStore: tokens,
      timeout: timeout,
    );

void main() {
  test('builds the URL with query parameters and skips empty ones', () async {
    late http.Request sent;
    final client = clientFor((request) async {
      sent = request;
      return jsonResponse({'ok': true});
    });

    final body = await client.getJson('/promotions', query: {'pageSize': '10', 'search': '', 'type': null});

    expect(body, {'ok': true});
    expect(sent.url.toString(), 'https://api.example.com/api/promotions?pageSize=10');
    expect(sent.headers['Accept'], 'application/json');
    expect(sent.headers.containsKey('Authorization'), isFalse);
  });

  test('attaches the stored bearer token over HTTPS', () async {
    late http.Request sent;
    final client = clientFor(
      (request) async {
        sent = request;
        return jsonResponse({});
      },
      tokens: InMemoryTokenStore('jwt-123'),
    );

    await client.getJson('/promotions');

    expect(sent.headers['Authorization'], 'Bearer jwt-123');
  });

  test('allows the token to the local development API', () async {
    late http.Request sent;
    final client = clientFor(
      (request) async {
        sent = request;
        return jsonResponse({});
      },
      baseUrl: 'http://10.0.2.2:5193/api',
      tokens: InMemoryTokenStore('jwt-123'),
    );

    await client.getJson('/promotions');

    expect(sent.headers['Authorization'], 'Bearer jwt-123');
  });

  test('refuses to send the token over plain HTTP to a remote host', () async {
    var called = false;
    final client = clientFor(
      (request) async {
        called = true;
        return jsonResponse({});
      },
      baseUrl: 'http://api.example.com/api',
      tokens: InMemoryTokenStore('jwt-123'),
    );

    await expectLater(
      client.getJson('/promotions'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.insecureConnection)),
    );
    expect(called, isFalse);
  });

  test('maps ProblemDetails detail into the error message', () async {
    final client = clientFor((_) async => jsonResponse(
          {'status': 409, 'title': 'Conflict', 'detail': 'Promotion has expired.'},
          409,
        ));

    await expectLater(
      client.getJson('/promotions/x'),
      throwsA(isA<ApiException>()
          .having((e) => e.kind, 'kind', ApiErrorKind.conflict)
          .having((e) => e.message, 'message', 'Promotion has expired.')),
    );
  });

  test('uses a friendly message for 404 without detail', () async {
    final client = clientFor((_) async => jsonResponse({'status': 404, 'title': 'Not Found'}, 404));

    await expectLater(
      client.getJson('/promotions/x'),
      throwsA(isA<ApiException>()
          .having((e) => e.kind, 'kind', ApiErrorKind.notFound)
          .having((e) => e.message, 'message', 'This item is no longer available.')),
    );
  });

  test('never exposes server error details', () async {
    final client = clientFor((_) async => jsonResponse({'detail': 'NullReferenceException at line 42'}, 500));

    await expectLater(
      client.getJson('/promotions'),
      throwsA(isA<ApiException>()
          .having((e) => e.kind, 'kind', ApiErrorKind.server)
          .having((e) => e.message, 'message', isNot(contains('NullReference')))),
    );
  });

  test('clears the stored token when the API answers 401', () async {
    final tokens = InMemoryTokenStore('expired');
    final client = clientFor((_) async => jsonResponse({}, 401), tokens: tokens);

    await expectLater(client.getJson('/promotions'), throwsA(isA<ApiException>()));
    expect(await tokens.read(), isNull);
  });

  test('maps connection failures and timeouts to network errors', () async {
    final offline = clientFor((_) async => throw http.ClientException('Connection refused'));
    final slow = clientFor(
      (_) => Future.delayed(const Duration(milliseconds: 200), () => jsonResponse({})),
      timeout: const Duration(milliseconds: 20),
    );

    for (final client in [offline, slow]) {
      await expectLater(
        client.getJson('/promotions'),
        throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.network)),
      );
    }
  });

  test('rejects a malformed response body', () async {
    final client = clientFor((_) async => http.Response('<html>', 200));

    await expectLater(
      client.getJson('/promotions'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.invalidResponse)),
    );
  });
}
