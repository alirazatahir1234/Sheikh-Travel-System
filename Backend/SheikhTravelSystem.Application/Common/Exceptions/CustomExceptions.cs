namespace SheikhTravelSystem.Application.Common.Exceptions;

public class NotFoundException(string name, object key)
    : Exception($"{name} with key '{key}' was not found.");

public class ConflictException(string message)
    : Exception(message);

public class ForbiddenException(string message = "You do not have permission to perform this action.")
    : Exception(message);
