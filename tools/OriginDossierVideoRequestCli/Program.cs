using Chummer.Run.AI.Services.Assets;

return await ProgramMain.MainAsync(args);

internal static class ProgramMain
{
    private const string RequestPathEnv = "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_VIDEO_REQUEST_PATH";

    public static async Task<int> MainAsync(string[] args)
    {
        string? envRequestPath = Environment.GetEnvironmentVariable(RequestPathEnv)?.Trim();
        string? requestPath = !string.IsNullOrWhiteSpace(envRequestPath)
            ? envRequestPath
            : (args.Length == 1 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null);

        if (requestPath is null)
        {
            Console.Error.WriteLine($"usage: origin-dossier-video-request-cli <request-path> or set {RequestPathEnv}");
            return 2;
        }

        var requests = new OriginDossierVideoRequestFileService();
        OriginDossierVideoRequestFileResult result = await requests.RenderFromFileAsync(requestPath);
        Console.WriteLine(result.ReceiptPath);
        return 0;
    }
}
