using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed partial class RobocopyService : IRobocopyService
{
    // Robocopy output is localized (Dateien/Files, Bytes/Bytes, Neue Datei/New File).
    // Position-based parsing keeps us locale-independent: summary rows are
    // "<label>: <int> <int> <int> <int> <int> <int>" lines in fixed order
    // (Dirs, Files, Bytes), file-event lines have "<label> <int> <path>".
    [GeneratedRegex(@"^\s*\S[^:]*:\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$")]
    private static partial Regex SummaryRowPattern();

    [GeneratedRegex(@"^\s+\S[^\d]+\s+(\d+)\s+(.+?)\s*$")]
    private static partial Regex FileEventPattern();

    public async Task<RobocopyResult> CopyAsync(
        string source,
        string target,
        RobocopyOptions options,
        IProgress<RobocopyProgress>? progress,
        CancellationToken ct)
    {
        var arguments = BuildArguments(source, target, options);

        var psi = new ProcessStartInfo("robocopy")
        {
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("robocopy konnte nicht gestartet werden.");

        var errors = new List<string>();
        long runningBytes = 0;
        long? finalFilesCopied = null;
        long? finalBytesCopied = null;
        var summaryRowsSeen = 0;

        var stopwatch = Stopwatch.StartNew();

        var outTask = Task.Run(async () =>
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line is null)
                {
                    break;
                }

                // Summary check must run before file-event check: summary rows also have
                // "<label>  <int>  ..." shape and would otherwise be mis-parsed as file events.
                var summaryMatch = SummaryRowPattern().Match(line);
                if (summaryMatch.Success)
                {
                    summaryRowsSeen++;
                    // Row 1 = Dirs (ignore), Row 2 = Files, Row 3 = Bytes. Group[2] is the "Copied" column.
                    if (summaryRowsSeen == 2
                        && long.TryParse(summaryMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copiedFiles))
                    {
                        finalFilesCopied = copiedFiles;
                    }
                    else if (summaryRowsSeen == 3
                             && long.TryParse(summaryMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copiedBytes))
                    {
                        finalBytesCopied = copiedBytes;
                    }
                    continue;
                }

                var fileMatch = FileEventPattern().Match(line);
                if (fileMatch.Success
                    && long.TryParse(fileMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
                {
                    runningBytes += bytes;
                    progress?.Report(new RobocopyProgress(fileMatch.Groups[2].Value, 0, runningBytes));
                }
            }
        }, ct);

        var errTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (line is null)
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(line))
                {
                    errors.Add(line);
                }
            }
        }, ct);

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        await Task.WhenAll(outTask, errTask);
        stopwatch.Stop();

        return new RobocopyResult(
            ExitCode: proc.ExitCode,
            FilesCopied: finalFilesCopied ?? 0,
            BytesCopied: finalBytesCopied ?? runningBytes,
            Elapsed: stopwatch.Elapsed,
            Errors: errors);
    }

    private static string BuildArguments(string source, string target, RobocopyOptions options)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(source.TrimEnd(Path.DirectorySeparatorChar)).Append("\" ");
        sb.Append('"').Append(target.TrimEnd(Path.DirectorySeparatorChar)).Append("\" ");

        if (options.Mirror)
        {
            sb.Append("/MIR ");
        }
        if (options.DryRun)
        {
            sb.Append("/L ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"/R:{options.Retries} /W:{options.WaitSeconds} ");
        sb.Append("/BYTES /NP /NDL /NJH ");

        return sb.ToString().TrimEnd();
    }
}
