namespace Nir5000.Core;

public sealed class LegacyIni
{
    private readonly Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);

    public static LegacyIni Load(string path)
    {
        var ini = new LegacyIni();
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                ini.sections.TryAdd(section, new(StringComparer.OrdinalIgnoreCase));
                continue;
            }
            var split = line.IndexOf('=');
            if (split <= 0) continue;
            ini.sections.TryAdd(section, new(StringComparer.OrdinalIgnoreCase));
            ini.sections[section][line[..split].Trim()] = line[(split + 1)..].Trim();
        }
        return ini;
    }

    public string? Get(string section, string key) =>
        sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : null;

    public int GetInt(string section, string key, int fallback = 0) =>
        int.TryParse(Get(section, key), out var value) ? value : fallback;

    public double GetDouble(string section, string key, double fallback = 0) =>
        double.TryParse(Get(section, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
