using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SurveyApp.Options;
using SurveyApp.Services;

namespace SurveyApp.Tests;

public sealed class SurveyConfigurationServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("survey-tests").FullName;
    private string FilePath => Path.Combine(_dir, "survey.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SurveyConfigurationService Create(bool reload = true) =>
        new(Microsoft.Extensions.Options.Options.Create(new SurveyOptions { ConfigurationFile = "survey.json", ReloadOnChange = reload }),
            new FakeEnvironment(_dir),
            NullLogger<SurveyConfigurationService>.Instance);

    [Fact]
    public void Startup_fails_clearly_when_the_file_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Create());
        Assert.Contains("missing or invalid", ex.Message);
    }

    [Fact]
    public void Startup_fails_clearly_when_the_file_is_invalid()
    {
        File.WriteAllText(FilePath, "{}");

        var ex = Assert.Throws<InvalidOperationException>(() => Create());
        Assert.Contains("surveyId is required", ex.Message);
    }

    [Fact]
    public async Task Valid_edit_is_applied_without_restart_and_invalid_edit_keeps_previous_version()
    {
        File.WriteAllText(FilePath, TestSupport.SurveyJson);
        using var service = Create();
        var original = service.Current;

        // Valid edit: new title is picked up, and the earlier instance is untouched (session consistency).
        File.WriteAllText(FilePath, TestSupport.SurveyJson.Replace("\"نموذج التقديم\"", "\"عنوان جديد\""));
        await WaitFor(() => service.Current.Header.Heading == "عنوان جديد");
        Assert.Equal("نموذج التقديم", original.Header.Heading);

        // Invalid edit: previous (edited) definition is retained.
        var good = service.Current;
        File.WriteAllText(FilePath, "{ broken json");
        await Task.Delay(1500);
        Assert.Same(good, service.Current);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++) await Task.Delay(100);
        Assert.True(condition(), "Timed out waiting for the configuration to reload.");
    }

    private sealed class FakeEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRoot;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = default!;
    }
}
