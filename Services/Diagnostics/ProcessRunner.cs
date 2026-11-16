using System.Diagnostics;
using System.IO;
using MochiSprout.Contracts;

namespace MochiSprout.Services.Diagnostics;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        var startedAt = DateTime.UtcNow;

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ProcessResult(-1, "", $"No se pudo iniciar '{fileName}': {ex.Message}", TimeSpan.Zero);
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var registration = cancellationToken.Register(() =>
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult(-1, await stdout, "Operación cancelada.", DateTime.UtcNow - startedAt);
        }

        return new ProcessResult(
            process.ExitCode,
            await stdout,
            await stderr,
            DateTime.UtcNow - startedAt);
    }
}
