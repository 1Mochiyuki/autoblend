using System.Diagnostics;
using Avalonia.Platform.Storage;

public record AudioFixOptions(bool IncludeDiscord, bool IncludeMusic, bool IncludeDesktop, bool UseMaster);

public record FixResult(int SuccessCount, int FailCount, List<string> Errors)
{
    public bool Success => FailCount == 0;
}

class ClipsHandler
{

    public string? UploadErrorMessage { get; private set; }
    private List<IStorageItem> ValidClips { get; } = [];
    private List<IStorageItem> InvalidClips { get; } = [];
    private List<IStorageItem> AudioFixedClips { get; } = [];
    private List<IStorageItem> FrameBlendedClips { get; } = [];


    public List<IStorageItem> ProcessUploadedClips(IStorageItem[] uploadedClips)
    {

        UploadErrorMessage = null;


        foreach (var file in uploadedClips)
        {
            if (file.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                ValidClips.Add(file);

            }
            else
            {

                InvalidClips.Add(file);
            }


        }
        if (InvalidClips.Count > 0)
        {
            UploadErrorMessage = InvalidClips.Count > 1 ? $"{InvalidClips.Count} files were of the wrong type and were ignored. Only .mp4 is supported" : $"\"{InvalidClips.First().Name}\" is of the wrong type and was ignored. Only .mp4 is supported";
        }
        return ValidClips;

    }


    public record ProcessResult(int SuccessCount, int FailCount, List<string> Errors)
    {
        public bool Success => FailCount == 0 && Errors.Count == 0;
    };

    public async Task<ProcessResult> FixClipAudioChannels(AudioFixOptions options)
    {
        var errors = new List<string>();
        var successCount = 0;
        foreach (var clip in ValidClips)
        {
            var inputPath = clip.Path.LocalPath;
            var outputPath = Path.Combine(
                Path.GetDirectoryName(inputPath)!,
                $"{Path.GetFileNameWithoutExtension(inputPath)}-AUDIO-FIX.mp4");

            try
            {
                string args;
                if (options.UseMaster)
                {
                    args = $"-i \"{inputPath}\" -map 0:v -map 0:a:5 -c:v copy -c:a aac \"{outputPath}\"";
                }
                else
                {
                    var inputs = "[0:a:0][0:a:1]";
                    var count = 2;
                    if (options.IncludeDiscord) { inputs += "[0:a:2]"; count++; }
                    if (options.IncludeMusic) { inputs += "[0:a:3]"; count++; }
                    if (options.IncludeDesktop) { inputs += "[0:a:4]"; count++; }

                    args = $"-i \"{inputPath}\" -filter_complex \"{inputs}amix=inputs={count}:duration=longest[aout]\" -map 0:v -map \"[aout]\" -c:v copy -c:a aac \"{outputPath}\"";
                }

                await RunToolAsync("ffmpeg", args);
                AudioFixedClips.Add(clip);
                successCount++;
            }
            catch (InvalidOperationException e)
            {
                errors.Add($"{clip.Name}: {e.Message}");
            }
        }
        ValidClips.Clear();
        InvalidClips.Clear();
        return new ProcessResult(successCount, errors.Count, errors);
    }


    public async Task<ProcessResult> FrameBlendClips()
    {
        var errors = new List<string>();
        var successCount = 0;
        foreach (var clip in ValidClips)
        {


            FrameBlendedClips.Add(clip);
        }

        AudioFixedClips.Clear();
        ValidClips.Clear();
        InvalidClips.Clear();

        return new ProcessResult(successCount, errors.Count, errors);
    }

    private static async Task<string> RunToolAsync(string toolName, string args)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = toolName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,

        };
        using var process = new Process { StartInfo = processInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{toolName} failed {error}");
        }
        return output;
    }

}
