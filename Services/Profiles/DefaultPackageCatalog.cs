namespace MochiSprout.Services.Profiles;

public sealed record CatalogPackage(string Id, string DisplayName, string RepositoryId);

public static class DefaultPackageCatalog
{
    public static List<CatalogPackage> Create() =>
    [
        new("nadena.dev.modular-avatar", "Modular Avatar", "nadena"),
        new("com.anatawa12.avatar-optimizer", "Avatar Optimizer", "anatawa12"),
        new("jp.lilxyzw.liltoon", "lilToon", "lilxyzw"),
        new("com.vrcfury.vrcfury", "VRCFury", "vrcfury")
    ];
}
