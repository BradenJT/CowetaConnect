namespace CowetaConnect.Domain.Exceptions;

public sealed class ForbiddenException(string message = "Access to this resource is forbidden.")
    : Exception(message);
