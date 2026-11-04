namespace Nir5000.Core;

public sealed record ScanRange(double StartNm, double EndNm, double IncrementNm)
{
    public IReadOnlyList<double> Wavelengths()
    {
        if (IncrementNm <= 0 || EndNm < StartNm) throw new InvalidOperationException("Invalid scan range.");
        var values = new List<double>();
        for (var value = StartNm; value <= EndNm + IncrementNm / 10; value += IncrementNm) values.Add(Math.Round(value, 6));
        return values;
    }
}

public sealed record Nir5000Profile(
    ScanRange ScanRange,
    int InstrumentType,
    int InstrumentPort,
    int BaudRateCode,
    int ReferenceScans,
    int SampleScans,
    double ReferenceOffset)
{
    public static Nir5000Profile Load(string path)
    {
        var ini = LegacyIni.Load(path);
        return new(
            new ScanRange(ini.GetDouble("ScanRange0", "Starting0", 1100), ini.GetDouble("ScanRange0", "Ending0", 2498), ini.GetDouble("ScanRange0", "Increment0", 2)),
            ini.GetInt("0", "InstrumentType", 11),
            ini.GetInt("0", "InstPort", 1),
            ini.GetInt("0", "BaudRate", 5),
            ini.GetInt("0", "RefScansToAveBefore", 10),
            ini.GetInt("0", "SampleScansToAve", 25),
            ini.GetDouble("0", "ReferenceOffset", 0));
    }
}
