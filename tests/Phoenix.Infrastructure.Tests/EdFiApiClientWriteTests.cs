using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phoenix.Core.Features;
using Phoenix.Core.Modules.EdFi;
using Phoenix.Infrastructure.EdFi;

namespace Phoenix.Infrastructure.Tests;

/// <summary>
/// PHX-7.1 write path. The 400 and 409 bodies are the ones nh-edfi-cus
/// (ODS/API 7.3, DS 6.1) actually returned on 2026-11-20 (correlation ids
/// replaced). The 403 body follows the errors[] shape the live API used for
/// its 401 response; its wording is illustrative.
/// </summary>
public class EdFiApiClientWriteTests
{
    private const string Status409UnresolvedReference =
        "{\"detail\":\"The referenced 'Student' item does not exist.\"," +
        "\"type\":\"urn:ed-fi:api:data-conflict:unresolved-reference\"," +
        "\"title\":\"Unresolved Reference\",\"status\":409," +
        "\"correlationId\":\"00000000-0000-0000-0000-000000000409\"}";

    private const string Status400Validation =
        "{\"detail\":\"Data validation failed. See 'validationErrors' for details.\"," +
        "\"type\":\"urn:ed-fi:api:bad-request:data-validation-failed\"," +
        "\"title\":\"Data Validation Failed\",\"status\":400," +
        "\"correlationId\":\"00000000-0000-0000-0000-000000000400\"," +
        "\"validationErrors\":{\"$.birthDate\":[\"BirthDate is required.\"]," +
        "\"$.firstName\":[\"FirstName is required.\"]}}";

    private const string Status403Forbidden =
        "{\"detail\":\"Access to the requested data could not be authorized.\"," +
        "\"type\":\"urn:ed-fi:api:security:authorization\"," +
        "\"title\":\"Authorization Denied\",\"status\":403," +
        "\"correlationId\":\"00000000-0000-0000-0000-000000000403\"," +
        "\"errors\":[\"No relationships have been established between the caller's education organization id claims and the resource item's EducationOrganizationId value.\"]}";

    [Fact]
    public async Task UpsertStudentAsync_posts_student_json_with_bearer()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9)));

        Assert.True(result.Success);
        Assert.True(result.Created);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(1, result.Attempts);
        Assert.Null(result.Problem);

        var sent = Assert.Single(handler.DataRequests);
        Assert.Equal("data/v3/ed-fi/students", sent.Path);
        Assert.Equal("test-token-123", sent.Bearer);

        using var json = JsonDocument.Parse(sent.Body);
        var root = json.RootElement;
        Assert.Equal("PHX-000123", root.GetProperty("studentUniqueId").GetString());
        Assert.Equal("Maria", root.GetProperty("firstName").GetString());
        Assert.Equal("Lopez", root.GetProperty("lastSurname").GetString());
        Assert.Equal("1998-04-09", root.GetProperty("birthDate").GetString());
        Assert.False(root.TryGetProperty("middleName", out _));
        Assert.False(root.TryGetProperty("generationCodeSuffix", out _));
    }

    [Fact]
    public async Task UpsertStudentSchoolAssociationAsync_sends_nested_references_and_scenario_fields()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentSchoolAssociationAsync(new EdFiEnrollmentWrite(
            StudentUniqueId: "PHX-000123",
            SchoolId: 220822001,
            EntryDate: new DateOnly(2026, 8, 17),
            EntryGradeLevelDescriptor: "uri://ed-fi.org/GradeLevelDescriptor#Adult Education",
            SchoolYear: 2027,
            CalendarCode: "220822001-2027",
            PrimarySchool: true,
            EnrollmentTypeDescriptor: "uri://ed-fi.org/EnrollmentTypeDescriptor#Current",
            FullTimeEquivalency: 1.0));

        Assert.True(result.Success);
        Assert.False(result.Created);
        Assert.Equal(200, result.StatusCode);

        var sent = Assert.Single(handler.DataRequests);
        Assert.Equal("data/v3/ed-fi/studentSchoolAssociations", sent.Path);

        using var json = JsonDocument.Parse(sent.Body);
        var root = json.RootElement;
        Assert.Equal("PHX-000123", root.GetProperty("studentReference").GetProperty("studentUniqueId").GetString());
        Assert.Equal(220822001, root.GetProperty("schoolReference").GetProperty("schoolId").GetInt64());
        Assert.Equal("2026-08-17", root.GetProperty("entryDate").GetString());
        Assert.Equal("uri://ed-fi.org/GradeLevelDescriptor#Adult Education", root.GetProperty("entryGradeLevelDescriptor").GetString());
        Assert.Equal(2027, root.GetProperty("schoolYearTypeReference").GetProperty("schoolYear").GetInt32());

        var calendar = root.GetProperty("calendarReference");
        Assert.Equal("220822001-2027", calendar.GetProperty("calendarCode").GetString());
        Assert.Equal(220822001, calendar.GetProperty("schoolId").GetInt64());
        Assert.Equal(2027, calendar.GetProperty("schoolYear").GetInt32());

        Assert.True(root.GetProperty("primarySchool").GetBoolean());
        Assert.Equal("uri://ed-fi.org/EnrollmentTypeDescriptor#Current", root.GetProperty("enrollmentTypeDescriptor").GetString());
        Assert.Equal(1.0, root.GetProperty("fullTimeEquivalency").GetDouble());
        Assert.False(root.TryGetProperty("exitWithdrawDate", out _));
    }

    [Fact]
    public async Task UpsertStudentSchoolAssociationAsync_rejects_calendar_without_school_year()
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertStudentSchoolAssociationAsync(
            new EdFiEnrollmentWrite(
                "PHX-000123",
                220822001,
                new DateOnly(2026, 8, 17),
                "uri://ed-fi.org/GradeLevelDescriptor#Adult Education",
                CalendarCode: "220822001-2027")));

        Assert.Empty(handler.DataRequests);
    }

    [Fact]
    public async Task UpsertStudentEducationOrganizationAssociationAsync_sends_edorg_reference()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentEducationOrganizationAssociationAsync(
            new EdFiStudentEdOrgWrite("PHX-000123", 220822));

        Assert.True(result.Success);

        var sent = Assert.Single(handler.DataRequests);
        Assert.Equal("data/v3/ed-fi/studentEducationOrganizationAssociations", sent.Path);

        using var json = JsonDocument.Parse(sent.Body);
        var root = json.RootElement;
        Assert.Equal("PHX-000123", root.GetProperty("studentReference").GetProperty("studentUniqueId").GetString());
        Assert.Equal(220822, root.GetProperty("educationOrganizationReference").GetProperty("educationOrganizationId").GetInt64());
    }

    [Fact]
    public async Task UpsertStudentIdentificationCodeAsync_sends_local_code()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentIdentificationCodeAsync(new EdFiStudentIdentificationCodeWrite(
            "PHX-000123",
            220822,
            "uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local",
            "PHX-000123"));

        Assert.True(result.Success);

        var sent = Assert.Single(handler.DataRequests);
        Assert.Equal("data/v3/ed-fi/studentIdentificationCodes", sent.Path);

        using var json = JsonDocument.Parse(sent.Body);
        var root = json.RootElement;
        Assert.Equal("uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local", root.GetProperty("studentIdentificationSystemDescriptor").GetString());
        Assert.Equal("PHX-000123", root.GetProperty("identificationCode").GetString());
        Assert.Equal(220822, root.GetProperty("educationOrganizationReference").GetProperty("educationOrganizationId").GetInt64());
        Assert.Equal("PHX-000123", root.GetProperty("studentReference").GetProperty("studentUniqueId").GetString());
        Assert.False(root.TryGetProperty("assigningOrganizationIdentificationCode", out _));
    }

    [Theory]
    [InlineData("uri://ed-fi.org/StudentIdentificationSystemDescriptor#SSN")]
    [InlineData("uri://ed-fi.org/StudentIdentificationSystemDescriptor#ssn ")]
    public async Task UpsertStudentIdentificationCodeAsync_never_sends_ssn(string descriptor)
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.UpsertStudentIdentificationCodeAsync(
            new EdFiStudentIdentificationCodeWrite("PHX-000123", 220822, descriptor, "000000000")));

        Assert.Empty(handler.DataRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Fact]
    public async Task Write_409_is_parsed_into_problem_and_not_retried()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.Conflict, Status409UnresolvedReference));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentSchoolAssociationAsync(new EdFiEnrollmentWrite(
            "PHX-NOPE", 220822001, new DateOnly(2026, 8, 17), "uri://ed-fi.org/GradeLevelDescriptor#Adult Education"));

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(1, result.Attempts);
        Assert.Single(handler.DataRequests);

        var problem = Assert.IsType<EdFiProblem>(result.Problem);
        Assert.Equal("urn:ed-fi:api:data-conflict:unresolved-reference", problem.Type);
        Assert.Equal("Unresolved Reference", problem.Title);
        Assert.Equal(409, problem.Status);
        Assert.Equal("The referenced 'Student' item does not exist.", problem.Detail);
        Assert.Equal("00000000-0000-0000-0000-000000000409", problem.CorrelationId);
        Assert.Empty(problem.Errors);
        Assert.Empty(problem.ValidationErrors);
    }

    [Fact]
    public async Task Write_400_validation_errors_are_parsed_and_not_retried()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.BadRequest, Status400Validation));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9)));

        Assert.False(result.Success);
        Assert.Equal(1, result.Attempts);
        Assert.Single(handler.DataRequests);

        var problem = Assert.IsType<EdFiProblem>(result.Problem);
        Assert.Equal("urn:ed-fi:api:bad-request:data-validation-failed", problem.Type);
        Assert.Equal(new[] { "FirstName is required." }, problem.ValidationErrors["$.firstName"]);
        Assert.Equal(new[] { "BirthDate is required." }, problem.ValidationErrors["$.birthDate"]);
        Assert.Contains("$.firstName: FirstName is required.", problem.Describe());
        Assert.Contains("Data Validation Failed (400)", problem.Describe());
    }

    [Fact]
    public async Task Write_403_errors_are_parsed_and_not_retried()
    {
        var handler = new ScriptedHandler(Respond(HttpStatusCode.Forbidden, Status403Forbidden));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentEducationOrganizationAssociationAsync(
            new EdFiStudentEdOrgWrite("PHX-000123", 220822));

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(1, result.Attempts);

        var problem = Assert.IsType<EdFiProblem>(result.Problem);
        Assert.Equal("urn:ed-fi:api:security:authorization", problem.Type);
        Assert.Single(problem.Errors);
    }

    [Fact]
    public async Task Write_503_is_retried_then_succeeds()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.ServiceUnavailable),
            Respond(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9)));

        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, handler.DataRequests.Count);
    }

    [Fact]
    public async Task Write_cold_start_404_retries_until_the_limit_then_reports_without_html()
    {
        const string coldStartPage = "<html><body>404 - Web app not found.</body></html>";
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.NotFound, coldStartPage, "text/html"),
            Respond(HttpStatusCode.NotFound, coldStartPage, "text/html"),
            Respond(HttpStatusCode.NotFound, coldStartPage, "text/html"));
        var client = CreateClient(handler, retryCount: 2);

        var result = await client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9)));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, handler.DataRequests.Count);

        var problem = Assert.IsType<EdFiProblem>(result.Problem);
        Assert.Equal(404, problem.Status);
        Assert.DoesNotContain("<html>", problem.Detail ?? string.Empty);
    }

    [Fact]
    public async Task Write_401_refreshes_token_once_and_retries()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.Unauthorized),
            Respond(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9)));

        Assert.True(result.Success);
        Assert.Equal(2, handler.TokenRequests);
        Assert.Equal(2, handler.DataRequests.Count);
    }

    [Fact]
    public async Task Writes_throw_when_flag_disabled()
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler, enabled: false);

        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.UpsertStudentAsync(
            new EdFiStudentWrite("PHX-000123", "Maria", "Lopez", new DateOnly(1998, 4, 9))));
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.UpsertStudentSchoolAssociationAsync(
            new EdFiEnrollmentWrite("PHX-000123", 220822001, new DateOnly(2026, 8, 17), "uri://ed-fi.org/GradeLevelDescriptor#Adult Education")));
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.UpsertStudentEducationOrganizationAssociationAsync(
            new EdFiStudentEdOrgWrite("PHX-000123", 220822)));
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.UpsertStudentIdentificationCodeAsync(
            new EdFiStudentIdentificationCodeWrite("PHX-000123", 220822, "uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local", "PHX-000123")));

        Assert.Empty(handler.DataRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    private static EdFiApiClient CreateClient(ScriptedHandler handler, bool enabled = true, int retryCount = 3)
    {
        var options = Options.Create(new EdFiApiOptions
        {
            BaseUrl = "https://edfi.test",
            Key = "key",
            Secret = "secret",
            WriteRetryCount = retryCount,
            WriteRetryDelayMs = 0
        });
        return new EdFiApiClient(
            new StubHttpClientFactory(handler),
            options,
            new StubFeatureFlags(enabled),
            NullLogger<EdFiApiClient>.Instance);
    }

    private static Func<HttpResponseMessage> Respond(
        HttpStatusCode status,
        string? body = null,
        string mediaType = "application/json") =>
        () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, mediaType)
        };

    private sealed record SentRequest(string Path, string? Bearer, string Body);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubFeatureFlags(bool enabled) : IFeatureFlagService
    {
        public bool IsEnabled(string featureName) => enabled;
        public IReadOnlyDictionary<string, FeatureFlagState> GetAll() =>
            new Dictionary<string, FeatureFlagState>();
        public void RequireEnabled(string featureName)
        {
            if (!enabled) throw new FeatureNotEnabledException(featureName);
        }
    }

    /// <summary>
    /// Answers the token endpoint itself and replays the scripted responses,
    /// in order, for data requests. Records every data request.
    /// </summary>
    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new(responses);

        public int TokenRequests { get; private set; }
        public List<SentRequest> DataRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/');

            if (path.Equals("oauth/token", StringComparison.OrdinalIgnoreCase))
            {
                TokenRequests++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"test-token-123\",\"expires_in\":1800,\"token_type\":\"bearer\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var bearer = request.Headers.Authorization is { Scheme: "Bearer" } auth ? auth.Parameter : null;
            DataRequests.Add(new SentRequest(path, bearer, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException($"No scripted response left for {path}.");

            return _responses.Dequeue()();
        }
    }
}
