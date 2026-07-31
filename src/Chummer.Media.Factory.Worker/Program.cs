using Chummer.Run.AI.Services.Assets;
using System.Text;
using System.Text.Json;

var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Dictionary<string, string> arguments = ParseArguments(args);
string inboxRoot = Resolve("inbox", "CHUMMER_MEDIA_FACTORY_ORIGIN_INBOX");
string receiptRoot = Resolve("receipts", "CHUMMER_MEDIA_FACTORY_ORIGIN_RECEIPTS");
string outputRoot = Resolve("output", "CHUMMER_MEDIA_FACTORY_ORIGIN_OUTPUTS");
string healthPath = Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_HEALTH_PATH")
    ?? Path.Combine(receiptRoot, "worker-health.json");
string sourceRootsValue = Resolve("source-roots", "CHUMMER_MEDIA_FACTORY_ALLOWED_SOURCE_ROOTS");
string[] sourceRoots = sourceRootsValue
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
string scriptPath = arguments.TryGetValue("magicfit-script", out string? configuredScript)
    ? configuredScript
    : Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_MAGICFIT_SCRIPT")
      ?? Path.Combine(AppContext.BaseDirectory, "providers", "render_magicfit_origin_dossier.py");
bool once = arguments.ContainsKey("once")
    || string.Equals(
        Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_WORKER_ONCE"),
        "true",
        StringComparison.OrdinalIgnoreCase);
int pollSeconds = ParsePositiveInt(
    arguments.GetValueOrDefault("poll-seconds")
    ?? Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_POLL_SECONDS"),
    fallback: 5,
    maximum: 300);
int maximumRequests = ParsePositiveInt(
    arguments.GetValueOrDefault("maximum-requests")
    ?? Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_MAX_REQUESTS_PER_POLL"),
    fallback: 10,
    maximum: 100);

using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(3) };
var narrationRenderer = new UnmixrOriginDossierAudiobookRenderer(httpClient);
var processor = new OriginDossierMediaInboxProcessor(
    inboxRoot,
    receiptRoot,
    outputRoot,
    sourceRoots,
    narrationRenderer,
    new MagicFitOriginDossierCinematicSceneRenderer(
        scriptPath,
        narrationRenderer));
using CancellationTokenSource stopping = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopping.Cancel();
};

do
{
    IReadOnlyList<Chummer.Media.Contracts.OriginDossierMediaDispatchReceipt> receipts =
        await processor.ProcessPendingAsync(maximumRequests, stopping.Token);
    WriteHealth(healthPath, receipts, utf8NoBom);
    if (receipts.Count > 0)
    {
        Console.WriteLine(
            $"origin-dossier-media: processed={receipts.Count} succeeded={receipts.Count(item => item.Status == "succeeded")} failed={receipts.Count(item => item.Status == "failed")}");
    }

    if (once)
    {
        break;
    }

    await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stopping.Token);
}
while (!stopping.IsCancellationRequested);

return;

string Resolve(string argumentName, string environmentName)
{
    string value = arguments.GetValueOrDefault(argumentName)
        ?? Environment.GetEnvironmentVariable(environmentName)
        ?? string.Empty;
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"origin_dossier_media_configuration_missing:{environmentName}");
    }

    return value.Trim();
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int index = 0; index < values.Length; index++)
    {
        string item = values[index];
        if (!item.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument: {item}");
        }

        string name = item[2..];
        if (name is "once")
        {
            parsed[name] = "true";
            continue;
        }

        if (++index >= values.Length)
        {
            throw new ArgumentException($"Missing value for --{name}.");
        }

        parsed[name] = values[index];
    }

    return parsed;
}

static int ParsePositiveInt(string? value, int fallback, int maximum)
    => int.TryParse(value, out int parsed)
        ? Math.Clamp(parsed, 1, maximum)
        : fallback;

static void WriteHealth(
    string path,
    IReadOnlyList<Chummer.Media.Contracts.OriginDossierMediaDispatchReceipt> receipts,
    Encoding encoding)
{
    path = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
    File.WriteAllText(
        temporaryPath,
        JsonSerializer.Serialize(
            new
            {
                contractVersion = "chummer.origin_dossier_media_worker_health.v1",
                status = "ready",
                processed = receipts.Count,
                succeeded = receipts.Count(item => item.Status == "succeeded"),
                failed = receipts.Count(item => item.Status == "failed"),
                observedAtUtc = DateTimeOffset.UtcNow
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }) + Environment.NewLine,
        encoding);
    File.Move(temporaryPath, path, true);
}
