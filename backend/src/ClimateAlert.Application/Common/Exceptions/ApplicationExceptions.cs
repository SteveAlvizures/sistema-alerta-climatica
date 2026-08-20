namespace ClimateAlert.Application.Common.Exceptions;

public sealed class ValidationException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);
