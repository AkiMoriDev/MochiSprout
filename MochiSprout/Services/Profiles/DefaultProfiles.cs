using MochiSprout.Models;

namespace MochiSprout.Services.Profiles;

public static class DefaultProfiles
{
    public static List<ProjectProfile> Create() =>
    [
        new ProjectProfile
        {
            Id = "avatar2022-base",
            DisplayName = "Avatar 2022 - Base",
            Target = UnityTarget.Unity2022
        },
        new ProjectProfile
        {
            Id = "avatar2022-modular",
            DisplayName = "Avatar 2022 - Modular y optimizado",
            Target = UnityTarget.Unity2022,
            Repositories =
            [
                new RepositoryEntry { Id = "nadena", DisplayName = "Modular Avatar", Url = "https://vpm.nadena.dev/vpm.json" },
                new RepositoryEntry { Id = "anatawa12", DisplayName = "Avatar Optimizer", Url = "https://vpm.anatawa12.com/vpm.json" },
                new RepositoryEntry { Id = "lilxyzw", DisplayName = "lilToon", Url = "https://lilxyzw.github.io/vpm-repos/vpm.json" }
            ],
            Packages =
            [
                new PackageEntry { Id = "nadena.dev.modular-avatar" },
                new PackageEntry { Id = "com.anatawa12.avatar-optimizer" },
                new PackageEntry { Id = "jp.lilxyzw.liltoon" }
            ]
        },
        new ProjectProfile
        {
            Id = "avatar2022-vrcfury",
            DisplayName = "Avatar 2022 - VRCFury",
            Target = UnityTarget.Unity2022,
            Repositories =
            [
                new RepositoryEntry { Id = "vrcfury", DisplayName = "VRCFury", Url = "https://vcc.vrcfury.com" }
            ],
            Packages =
            [
                new PackageEntry { Id = "com.vrcfury.vrcfury" }
            ]
        },
        new ProjectProfile
        {
            Id = "avatar2019-base",
            DisplayName = "Avatar 2019 - Base Legacy",
            Target = UnityTarget.Unity2019Legacy,
            Notes = "Perfil de referencia para Unity 2019 Legacy."
        }
    ];
}
