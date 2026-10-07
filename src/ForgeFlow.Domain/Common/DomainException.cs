namespace ForgeFlow.Domain.Common;

/// <summary>A business rule was violated by the requested operation.</summary>
public class DomainException(string message) : Exception(message);
