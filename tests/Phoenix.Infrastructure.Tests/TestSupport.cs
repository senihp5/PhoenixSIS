using Microsoft.Extensions.Options;
using Phoenix.Core.Auditing;
using Phoenix.Infrastructure.Auditing;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

/// <summary>Shared builders for services that need the ADR-004 audit trail.</summary>
internal static class TestSupport
{
    public static readonly AuditActor TestActor = new("00000000-0000-0000-0000-0000000000aa", "tester@phoenixsis.test");

    public static AuditLog Audit(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, user ?? new FakeCurrentUser(TestActor));

    public static StudentIdentityService Identity(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, Audit(db, user));

    public const string Campus = "220822001";
    public const string SecondCampus = "220822002";

    public static CampusDirectory Campuses() =>
        new(Options.Create(new PhoenixCampusOptions
        {
            Campuses =
            [
                new CampusOption { Number = Campus, Name = "New Heights Adult High School", LeaNumber = "220822" },
                new CampusOption { Number = SecondCampus, Name = "Second Test Campus", LeaNumber = "220822" },
                new CampusOption { Number = "BAD", Name = "Ignored - not 9 digits", LeaNumber = "220822" }
            ]
        }));

    public static EnrollmentService Enrollments(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, Audit(db, user), Campuses());
}

internal sealed class FakeCurrentUser(AuditActor actor) : ICurrentUser
{
    public ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(actor);
}

/// <summary>An actor source that always fails - proves ADR-004 fail closed.</summary>
internal sealed class FailingCurrentUser : ICurrentUser
{
    public ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Audit store unavailable.");
}
