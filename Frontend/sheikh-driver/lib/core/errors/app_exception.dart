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
    super.message = defaultMessage,
  ]);

  static const defaultMessage = 'Session expired — please sign in again.';
}

class RateLimitException extends AppException {
  const RateLimitException([
    super.message = 'Too many attempts. Please wait a minute and try again.',
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

class ForbiddenException extends AppException {
  const ForbiddenException([
    super.message = defaultMessage,
  ]);

  static const defaultMessage =
      'Access restricted — your account role or tenant plan does not include this permission. '
      'Try signing out and back in if permissions were recently updated.';

  /// Attendance-specific copy required by product (never show raw Dio 403).
  static const attendance = ForbiddenException(
    "You don't have permission to access this attendance information. Please contact your company administrator.",
  );
}

class ValidationException extends AppException {
  const ValidationException(super.message);
}

class ConflictException extends AppException {
  const ConflictException(super.message);
}

class UnknownException extends AppException {
  const UnknownException([
    super.message = 'An unexpected error occurred.',
  ]);
}
