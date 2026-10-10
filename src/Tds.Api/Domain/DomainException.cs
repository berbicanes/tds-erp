namespace Tds.Api.Domain;

/// <summary>A business rule was violated. <see cref="Exception.Message"/> is user-facing Bosnian text.</summary>
public class DomainException(string message) : Exception(message);
