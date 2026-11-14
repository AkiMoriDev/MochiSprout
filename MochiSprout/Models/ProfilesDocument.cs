namespace MochiSprout.Models;

public sealed class ProfilesDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<ProjectProfile> Profiles { get; set; } = [];
}
