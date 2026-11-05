namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// A minimal student projection for display. A Phoenix.Core contract — not
/// the full Ed-Fi student resource — so the UI depends only on what it shows.
/// </summary>
public sealed record EdFiStudentSummary(
    string StudentUniqueId,
    string FirstName,
    string LastSurname,
    string? BirthDate);
