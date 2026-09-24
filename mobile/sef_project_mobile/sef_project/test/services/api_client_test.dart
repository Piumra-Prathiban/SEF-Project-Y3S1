import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sef_project/services/api_client.dart';

void main() {
  group('ApiClient', () {
    test('sends the bearer token and decodes the JSON body', () async {
      late http.Request captured;

      final client = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response(jsonEncode({'ok': true}), 200);
        }),
        baseUrl: 'http://localhost:5193/api',
      );

      final data = await client.get('/Orders?page=2', token: 'test-token');

      expect(captured.method, 'GET');
      expect(
        captured.url.toString(),
        'http://localhost:5193/api/Orders?page=2',
      );
      expect(captured.headers['Authorization'], 'Bearer test-token');
      expect((data as Map<String, dynamic>)['ok'], isTrue);
    });

    test('omits the Authorization header when no token is supplied', () async {
      late http.Request captured;

      final client = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response(jsonEncode({'ok': true}), 200);
        }),
        baseUrl: 'http://localhost:5193/api',
      );

      await client.get('/Orders');

      expect(captured.headers.containsKey('Authorization'), isFalse);
    });

    test('maps a ProblemDetails failure to an ApiException with the status',
        () async {
      final client = ApiClient(
        httpClient: MockClient(
          (_) async =>
              http.Response(jsonEncode({'detail': 'Order is not visible.'}), 404),
        ),
        baseUrl: 'http://localhost:5193/api',
      );

      await expectLater(
        client.get('/Orders/1', token: 'test-token'),
        throwsA(
          isA<ApiException>()
              .having((error) => error.status, 'status', 404)
              .having((error) => error.message, 'message', 'Order is not visible.'),
        ),
      );
    });

    test('falls back to the ProblemDetails title, then a generic message',
        () async {
      final client = ApiClient(
        httpClient: MockClient(
          (_) async => http.Response(jsonEncode({'title': 'Bad Request'}), 400),
        ),
        baseUrl: 'http://localhost:5193/api',
      );

      await expectLater(
        client.post('/Auth/login', body: const {}),
        throwsA(
          isA<ApiException>()
              .having((error) => error.status, 'status', 400)
              .having((error) => error.message, 'message', 'Bad Request'),
        ),
      );
    });

    test('maps a network failure to status 0 with a friendly message',
        () async {
      final client = ApiClient(
        httpClient: MockClient((_) async => throw Exception('network down')),
        baseUrl: 'http://localhost:5193/api',
      );

      await expectLater(
        client.get('/Orders', token: 'test-token'),
        throwsA(
          isA<ApiException>()
              .having((error) => error.status, 'status', 0)
              .having(
                (error) => error.message,
                'message',
                contains('Could not reach the server'),
              ),
        ),
      );
    });
  });
}
