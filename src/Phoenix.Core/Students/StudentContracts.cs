namespace Phoenix.Core.Students;

/// <summary>Details used to create a canonical Phoenix student.</summary>
public sealed record StudentDetails(
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly? BirthDate);

/// <summary>A canonical Phoenix student as seen by callers (no EF types).</summary>
public sealed record StudentRecord(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly? BirthDate);
