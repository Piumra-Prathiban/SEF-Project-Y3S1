import 'dart:convert';

enum ApiErrorKind {
  network,
  unauthorized,
  forbidden,
  notFound,
  badRequest,
  conflict,
  server,
  insecureConnection,
  invalidResponse,
}

/// An API failure with a message that is safe to show to customers.
class ApiException implements Exception {
  const ApiException({
    required this.kind,
    required this.message,
    this.statusCode,
  });

  const ApiException.network(String message)
      : this(kind: ApiErrorKind.network, message: message);

  /// Maps an HTTP error response (ASP.NET Core ProblemDetails) to an exception.
  factory ApiException.fromResponse(int statusCode, String body) {
    final kind = switch (statusCode) {
      400 => ApiErrorKind.badRequest,
      401 => ApiErrorKind.unauthorized,
      403 => ApiErrorKind.forbidden,
      404 => ApiErrorKind.notFound,
      409 => ApiErrorKind.conflict,
      _ => ApiErrorKind.server,
    };

    // `detail` carries the API's business-rule message; `title` is only the
    // generic status text ("Not Found"), so it is not shown.
    String? detail;
    try {
      final decoded = jsonDecode(body);
      if (decoded is Map<String, dynamic> && decoded['detail'] is String) {
        detail = decoded['detail'] as String;
      }
    } on FormatException {
      detail = null;
    }

    // Server errors never expose internal details.
    final message = kind == ApiErrorKind.server
        ? 'The server had a problem. Please try again later.'
        : (detail != null && detail.isNotEmpty ? detail : _defaultMessage(kind));

    return ApiException(kind: kind, message: message, statusCode: statusCode);
  }

  final ApiErrorKind kind;
  final String message;
  final int? statusCode;

  static String _defaultMessage(ApiErrorKind kind) => switch (kind) {
        ApiErrorKind.unauthorized => 'Please sign in again.',
        ApiErrorKind.forbidden => 'You do not have access to this.',
        ApiErrorKind.notFound => 'This item is no longer available.',
        ApiErrorKind.badRequest => 'The request was not valid.',
        ApiErrorKind.conflict => 'This action is not possible right now.',
        _ => 'Something went wrong. Please try again.',
      };

  @override
  String toString() => 'ApiException($kind, $statusCode): $message';
}

/// A customer-friendly message for any error thrown while loading data.
String describeError(Object error) {
  if (error is ApiException) {
    return error.message;
  }
  return 'Something went wrong. Please try again.';
}
