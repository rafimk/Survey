using System.Text.Json;
using Microsoft.Extensions.Options;
using SurveyApp.Models;
using SurveyApp.Options;

namespace SurveyApp.Services;

/// <summary>
/// Loads and caches the survey definition. Fails construction (and therefore startup) if the initial file is
/// invalid; later invalid edits are logged and the last valid definition is kept.
/// </summary>
public sealed class SurveyConfigurationService : ISurveyConfigurationService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private readonly ILogger<SurveyConfigurationService> _logger;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer? _debounce;
    private volatile SurveyDefinition _current;

    public SurveyConfigurationService(
        IOptions<SurveyOptions> options,
        IHostEnvironment environment,
        ILogger<SurveyConfigurationService> logger)
    {
        _logger = logger;
        var file = options.Value.ConfigurationFile;
        _path = Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(environment.ContentRootPath, file));

        var (definition, errors) = LoadFile(_path);
        if (definition is null)
        {
            throw new InvalidOperationException(
                $"The survey configuration '{_path}' is missing or invalid:{Environment.NewLine} - " +
                string.Join($"{Environment.NewLine} - ", errors));
        }
        _current = definition;
        _logger.LogInformation("Loaded survey '{SurveyId}' v{Version} from {Path}.", definition.SurveyId, definition.Version, _path);

        if (options.Value.ReloadOnChange)
        {
            _debounce = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_path)!, Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            _watcher.Changed += (_, _) => ScheduleReload();
            _watcher.Created += (_, _) => ScheduleReload();
            _watcher.Renamed += (_, _) => ScheduleReload();
            _watcher.EnableRaisingEvents = true;
        }
    }

    public SurveyDefinition Current => _current;

    /// <summary>Parses and validates survey JSON. Returns a definition, or the list of problems.</summary>
    public static (SurveyDefinition? Definition, List<string> Errors) Parse(string json)
    {
        SurveyDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<SurveyDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return (null, [$"Invalid JSON: {ex.Message}"]);
        }

        if (definition is null) return (null, ["The survey file is empty."]);

        var errors = SurveyDefinitionValidator.Validate(definition);
        return errors.Count == 0 ? (definition, errors) : (null, errors);
    }

    private static (SurveyDefinition? Definition, List<string> Errors) LoadFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, [$"Cannot read file: {ex.Message}"]);
        }
    }

    // Editors raise several events per save; wait for them to settle.
    private void ScheduleReload() => _debounce?.Change(DebounceDelay, Timeout.InfiniteTimeSpan);

    private void Reload()
    {
        var (definition, errors) = LoadFile(_path);
        if (definition is null)
        {
            _logger.LogError(
                "Ignoring invalid survey configuration edit; keeping the previous version. Problems: {Errors}",
                string.Join(" | ", errors));
            return;
        }

        _current = definition;
        _logger.LogInformation("Reloaded survey '{SurveyId}' v{Version}.", definition.SurveyId, definition.Version);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
