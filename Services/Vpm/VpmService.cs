using MochiSprout.Contracts;

namespace MochiSprout.Services.Vpm;

public sealed class VpmService
{
    private const string Executable = "vpm";
    private readonly IProcessRunner _runner;

    public VpmService(IProcessRunner runner) => _runner = runner;

    public Task<ProcessResult> ListReposAsync(CancellationToken ct) =>
        _runner.RunAsync(Executable, ["list", "repos"], null, ct);
}
