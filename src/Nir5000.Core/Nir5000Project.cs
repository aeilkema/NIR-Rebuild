namespace Nir5000.Core;

public sealed record Nir5000Product(
    int Index,
    int ProductCode,
    string Description,
    bool IsCheckCell,
    ScanRange ScanRange);

public sealed record Nir5000Project(
    string SourcePath,
    Nir5000Profile Profile,
    IReadOnlyList<Nir5000Product> Products)
{
    public static Nir5000Project Load(string path)
    {
        var ini = LegacyIni.Load(path);
        var profile = Nir5000Profile.Load(path);
        var count = Math.Max(0, ini.GetInt("Stats", "NumProducts", 0));
        var products = new List<Nir5000Product>(count);
        for (var index = 0; index < count; index++)
        {
            var section = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var rangeSection = $"ScanRange{index}";
            var range = new ScanRange(
                ini.GetDouble(rangeSection, "Starting0", profile.ScanRange.StartNm),
                ini.GetDouble(rangeSection, "Ending0", profile.ScanRange.EndNm),
                ini.GetDouble(rangeSection, "Increment0", profile.ScanRange.IncrementNm));
            products.Add(new Nir5000Product(
                index,
                ini.GetInt(section, "ProductCode", index),
                ini.Get(section, "Description") ?? string.Empty,
                ini.GetInt(section, "Check cell", 0) != 0,
                range));
        }
        return new Nir5000Project(path, profile, products);
    }
}
