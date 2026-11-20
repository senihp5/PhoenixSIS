namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// An Ed-Fi StudentSchoolAssociation (enrollment). Natural key: student,
/// school, entry date. DS 6.1 requires only the student, school, entry date
/// and entry grade level; the rest are the scenario fields a real SIS sends.
/// A calendar reference needs the school year as well as the calendar code.
/// </summary>
public sealed record EdFiEnrollmentWrite(
    string StudentUniqueId,
    long SchoolId,
    DateOnly EntryDate,
    string EntryGradeLevelDescriptor,
    int? SchoolYear = null,
    string? CalendarCode = null,
    bool? PrimarySchool = null,
    string? EnrollmentTypeDescriptor = null,
    double? FullTimeEquivalency = null,
    DateOnly? ExitWithdrawDate = null,
    string? ExitWithdrawTypeDescriptor = null);
