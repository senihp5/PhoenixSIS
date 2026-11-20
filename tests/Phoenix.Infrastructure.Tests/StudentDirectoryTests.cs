using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Features;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

/// <summary>PHX-8.3 audited student directory (Students flag).</summary>
public class StudentDirectoryTests
{
    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static StudentDirectory NewDirectory(PhoenixDbContext db, bool enabled = true, ICurrentUser? user = null) =>
        new(db, TestSupport.Audit(db, user), TestSupport.Enrollments(db), new Flags(enabled));

    private static async Task<List<Guid>> SeedAsync(PhoenixDbContext db, int count)
    {
        var identity = TestSupport.Identity(db);
        var ids = new List<Guid>();
        for (var i = 1; i <= count; i++)
        {
            var s = await identity.ResolveOrCreateAsync(
                "Sample", $"S-{i:D3}", new StudentDetails($"First{i:D3}", null, $"Last{i:D3}", new DateOnly(1990, 1, 1)));
            ids.Add(s.Id);
        }

        return ids;
    }

    [Fact]
    public async Task Search_pages_by_25_and_records_one_view_per_student_shown()
    {
        await using var db = NewDb();
        await SeedAsync(db, 30);
        var dir = NewDirectory(db);

        var page1 = await dir.SearchAsync(null, 2027, 1);
        var page2 = await dir.SearchAsync(null, 2027, 2);

        Assert.Equal(30, page1.Total);
        Assert.Equal(2, page1.PageCount);
        Assert.Equal(25, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal("Last001", page1.Items[0].LastName);

        var views = await db.AuditEvents.Where(e => e.Action == AuditAction.View).ToListAsync();
        Assert.Equal(30, views.Count);
        Assert.Equal(30, views.Select(v => v.StudentId).Distinct().Count());
        Assert.All(views, v => Assert.Equal("Listed in student search.", v.Summary));
    }

    [Fact]
    public async Task Search_matches_name_or_identifier_and_shows_the_year_enrollment()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, 3);
        await TestSupport.Identity(db).AddIdentifierAsync(ids[1], "Ed-Fi", "PHX-000042");
        await TestSupport.Enrollments(db).EnrollAsync(
            new NewEnrollment(ids[1], TestSupport.Campus, 2027, new DateOnly(2026, 8, 17), GradeLevel.AdultEducation));
        var dir = NewDirectory(db);

        var byId = await dir.SearchAsync("PHX-000042", 2027, 1);
        var byName = await dir.SearchAsync("Last003", 2027, 1);

        var hit = Assert.Single(byId.Items);
        Assert.Equal(ids[1], hit.Id);
        Assert.Equal("PHX-000042", hit.EdFiId);
        Assert.NotNull(hit.YearEnrollment);
        Assert.Equal(TestSupport.Campus, hit.YearEnrollment!.CampusNumber);

        var other = Assert.Single(byName.Items);
        Assert.Null(other.YearEnrollment);
    }

    [Fact]
    public async Task Opening_a_record_records_one_view()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, 1);
        var dir = NewDirectory(db);

        var detail = await dir.GetAsync(ids[0]);

        Assert.NotNull(detail);
        Assert.Contains(detail!.Identifiers, i => i.SourceSystem == "Sample");
        var view = await db.AuditEvents.SingleAsync(e => e.Action == AuditAction.View);
        Assert.Equal(ids[0], view.StudentId);
        Assert.Equal("Opened student record.", view.Summary);
    }

    [Fact]
    public async Task Missing_student_returns_null_and_records_nothing()
    {
        await using var db = NewDb();
        var dir = NewDirectory(db);

        Assert.Null(await dir.GetAsync(Guid.NewGuid()));
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task No_data_is_returned_when_the_view_cannot_be_recorded()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, 2);
        var dir = NewDirectory(db, user: new FailingCurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => dir.SearchAsync(null, 2027, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dir.GetAsync(ids[0]));
    }

    [Fact]
    public async Task Directory_requires_the_students_flag()
    {
        await using var db = NewDb();
        var dir = NewDirectory(db, enabled: false);

        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => dir.SearchAsync(null, 2027, 1));
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => dir.GetAsync(Guid.NewGuid()));
    }

    private sealed class Flags(bool enabled) : IFeatureFlagService
    {
        public bool IsEnabled(string featureName) => enabled;
        public IReadOnlyDictionary<string, FeatureFlagState> GetAll() => new Dictionary<string, FeatureFlagState>();
        public void RequireEnabled(string featureName)
        {
            if (!enabled) throw new FeatureNotEnabledException(featureName);
        }
    }
}
