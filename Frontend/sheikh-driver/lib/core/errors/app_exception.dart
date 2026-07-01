sealed class AppException implements Exception {
  const AppException(this.message);
  final String message;

  @override
  String toString() => message;
}

class NetworkException extends AppException {
  const NetworkException([
    super.message = 'No internet connection. Please check your network.',
  ]);
}

class AuthException extends AppException {
  const AuthException([
    super.message = 'Session expired. Please log in again.',
  ]);
}

class ServerException extends AppException {
  const ServerException([
    super.message = 'Server error. Please try again later.',
  ]);
}

class NotFoundException extends AppException {
  const NotFoundException([
    super.message = 'The requested resource was not found.',
  ]);
}

class ValidationException extends AppException {
  const ValidationException(super.message);
}

class UnknownException extends AppException {
  const UnknownException([
    super.message = 'An unexpected error occurred.',
  ]);
}
