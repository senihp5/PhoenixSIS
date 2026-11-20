using Microsoft.Extensions.Options;
using Phoenix.Core.Students;

namespace Phoenix.Infrastructure.Students;

/// <summary>Bound from the "Phoenix" configuration section (non-secret).</summary>
public sealed class PhoenixCampusOptions
{
    public const string SectionName = "Phoenix";

    public List<CampusOption> Campuses { get; set; } = [];
}

public sealed class CampusOption
{
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LeaNumber { get; set; } = string.Empty;
}

/// <summary>
/// Campuses from configuration (Sprint 8 decision: one campus today; a Campus
/// table waits for a second campus). Entries without a 9-digit number are
/// ignored.
/// </summary>
public sealed class CampusDirectory : ICampusDirectory
{
    private readonly IReadOnlyList<CampusInfo> _campuses;

    public CampusDirectory(IOptions<PhoenixCampusOptions> options)
    {
        _campuses = options.Value.Campuses
            .Where(c => c.Number.Length == 9 && c.Number.All(char.IsAsciiDigit))
            .Select(c => new CampusInfo(c.Number, c.Name, c.LeaNumber))
            .ToList();
    }

    public IReadOnlyList<CampusInfo> GetCampuses() => _campuses;

    public CampusInfo? Find(string campusNumber) =>
        _campuses.FirstOrDefault(c => c.Number == campusNumber?.Trim());
}
