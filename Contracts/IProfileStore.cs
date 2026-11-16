using MochiSprout.Models;

namespace MochiSprout.Contracts;

public interface IProfileStore
{
    Task<List<ProjectProfile>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(List<ProjectProfile> profiles, CancellationToken ct = default);
}
