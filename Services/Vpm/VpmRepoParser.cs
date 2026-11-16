namespace MochiSprout.Services.Vpm;

public static class VpmRepoParser
{
    /// <summary>
    /// La salida real de "vpm list repos" tiene el formato
    /// "[HH:mm:ss INF] repoId | Nombre (Autor)" y NO incluye URLs.
    /// Esto extrae únicamente los IDs de los repositorios activos/registrados.
    /// </summary>
    public static HashSet<string> ParseActiveIds(string standardOutput)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(standardOutput))
            return ids;

        foreach (var rawLine in standardOutput.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0) continue;

            var bracketEnd = line.IndexOf(']');
            var afterBracket = bracketEnd >= 0 ? line[(bracketEnd + 1)..] : line;

            var pipeIndex = afterBracket.IndexOf('|');
            if (pipeIndex < 0) continue;

            var id = afterBracket[..pipeIndex].Trim();
            if (id.Length > 0)
                ids.Add(id);
        }

        return ids;
    }
}
