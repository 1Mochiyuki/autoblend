using System.Diagnostics;
using System.Text.Json;
using Avalonia.Platform.Storage;

public record AudioFixOptions(bool CombineGameAudio, bool CombineMicAudio, bool CombineDiscordAudio, bool CombineMusicAudio, bool CombineDesktopAudio, bool UseMaster, bool RemoveEmptyAudioChannels);

public record FixResult(int SuccessCount, int FailCount, List<string> Errors)
{
    public bool IsSuccess => FailCount == 0;
}

class ClipsHandler
{

    public string? UploadErrorMessage { get; private set; }
    private List<IStorageItem> ValidClips { get; } = [];
    private List<IStorageItem> InvalidClips { get; } = [];
    private List<IStorageItem> AudioFixedClips { get; } = [];
    private List<IStorageItem> FrameBlendedClips { get; } = [];

    // channel tag name -> fallback audio stream index (channel N -> 0:a:N-1)
    private static readonly Dictionary<string, int> ChannelFallbackIndices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mic"] = 0,
        ["game"] = 1,
        ["discord"] = 2,
        ["music"] = 3,
        ["desktop"] = 4,
        ["master"] = 5,
    };

    private sealed record AudioStreamInfo(int AbsoluteIndex, string? Title);

    // ffprobe lists every audio stream with its absolute index and optional title tag.
    private async Task<List<AudioStreamInfo>?> ProbeAudioStreamsAsync(string inputPath, CancellationToken ct)
    {
        try
        {
            var args = $"-v error -select_streams a -show_entries stream=index:stream_tags=title -of json \"{inputPath}\"";
            var json = await RunToolAsync("ffprobe", args, ct);

            var streams = new List<AudioStreamInfo>();
            using var doc = JsonDocument.Parse(json);
            foreach (var stream in doc.RootElement.GetProperty("streams").EnumerateArray())
            {
                var index = stream.GetProperty("index").GetInt32();
                string? title = null;
                if (stream.TryGetProperty("tags", out var tags) &&
                    tags.TryGetProperty("title", out var titleTag) &&
                    titleTag.ValueKind == JsonValueKind.String)
                {
                    title = titleTag.GetString();
                }
                streams.Add(new AudioStreamInfo(index, title));
            }
            return streams;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    // resolves a channel to a stream selector: the stream tagged with the channel name
    // if one exists, otherwise the channel's fixed index
    private static string ResolveChannelInput(List<AudioStreamInfo>? audioStreams, string channelName)
    {
        var tagged = audioStreams?.FirstOrDefault(s =>
            string.Equals(s.Title?.Trim(), channelName, StringComparison.OrdinalIgnoreCase));
        return tagged is not null ? $"0:{tagged.AbsoluteIndex}" : $"0:a:{ChannelFallbackIndices[channelName]}";
    }


    public List<IStorageItem> ProcessUploadedClips(IStorageItem[] uploadedClips)
    {

        ValidClips.Clear();

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
        public bool IsSuccess => FailCount == 0 && Errors.Count == 0;
    };

    // Clip audio layout: mic = 0, game = 1, discord = 2, music = 3, desktop = 4, master track = 5 (audio stream indexes).
    // Each channel is resolved by its title tag ("mic", "game", ...) when the clip has one,
    // otherwise it falls back to the fixed index above.
    public async Task<ProcessResult> FixClipAudioChannels(AudioFixOptions options, CancellationToken ct)
    {

        Console.WriteLine("options: mic: {0}, game: {1}, discord: {2}, music: {3}, desktop: {4}, use master track: {5}",
                          options.CombineMicAudio,
                          options.CombineGameAudio,
                          options.CombineDiscordAudio,
                          options.CombineMusicAudio,
                          options.CombineDesktopAudio,
                          options.UseMaster);
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
                var audioStreams = await ProbeAudioStreamsAsync(inputPath, ct);

                string args;
                if (options.UseMaster)
                {
                    args = $"-i \"{inputPath}\" -map 0:v -map {ResolveChannelInput(audioStreams, "master")} -c:v copy -c:a aac \"{outputPath}\"";
                }
                else
                {
                    var selectedChannels = new List<string>();
                    if (options.CombineMicAudio) selectedChannels.Add("mic");
                    if (options.CombineGameAudio) selectedChannels.Add("game");
                    if (options.CombineDiscordAudio) selectedChannels.Add("discord");
                    if (options.CombineMusicAudio) selectedChannels.Add("music");
                    if (options.CombineDesktopAudio) selectedChannels.Add("desktop");

                    if (selectedChannels.Count == 0)
                    {
                        throw new InvalidOperationException("no audio channels were selected to combine");
                    }

                    var inputs = string.Concat(selectedChannels.Select(c => $"[{ResolveChannelInput(audioStreams, c)}]"));
                    args = $"-i \"{inputPath}\" -filter_complex \"{inputs}amix=inputs={selectedChannels.Count}:duration=longest[aout]\" -map 0:v -map \"[aout]\" -c:v copy -c:a aac \"{outputPath}\"";
                }

                await RunToolAsync("ffmpeg", args, ct);
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


    public async Task<ProcessResult> FrameBlendClips(CancellationToken ct)
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

    private static async Task<string> RunToolAsync(string toolName, string args, CancellationToken ct)
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
        using var register = ct.Register(() => process.Kill(entireProcessTree: true));
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{toolName} failed {error}");
        }
        return output;
    }

}
