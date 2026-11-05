namespace Phoenix.Infrastructure.Data;

/// <summary>
/// The canonical Phoenix student. Phoenix-native, source-agnostic: external
/// identifiers (Ed-Fi, PowerSchool, TSDS, …) are NOT columns here — they live
/// in <see cref="StudentIdentifier"/>. See ADR-002.
/// </summary>
public class Student
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<StudentIdentifier> Identifiers { get; set; } = new List<StudentIdentifier>();
}
