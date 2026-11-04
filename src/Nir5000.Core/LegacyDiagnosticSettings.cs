namespace Nir5000.Core;

public sealed record LegacyDiagnosticSettings(
    int ModelType,
    string SerialNumber,
    ScanRange VisibleRange,
    ScanRange NirRange,
    IReadOnlyList<double> ReferenceWavelengths,
    int ReferenceBefore,
    int ScanAverage,
    int ReferenceAfter,
    int Port,
    int BaudCode,
    double NirK,
    double NirPhi,
    double VisibleK,
    double VisiblePhi)
{
    public static LegacyDiagnosticSettings Load(string path)
    {
        var ini = LegacyIni.Load(path);
        var references = Enumerable.Range(1, 8)
            .Select(index => ini.GetDouble("Wavelength Section", $"WaveLength{index}", 0))
            .Where(value => value > 0)
            .ToArray();
        return new(
            ini.GetInt("Main Section", "ModelType", 9),
            ini.Get("Main Section", "SerialNumber") ?? string.Empty,
            new ScanRange(
                ini.GetDouble("Instrument Setup Section", "StartWave1", 400),
                ini.GetDouble("Instrument Setup Section", "EndWave1", 1098),
                ini.GetDouble("Instrument Setup Section", "IncrWave1", 2)),
            new ScanRange(
                ini.GetDouble("Instrument Setup Section", "StartWave2", 1100),
                ini.GetDouble("Instrument Setup Section", "EndWave2", 2498),
                ini.GetDouble("Instrument Setup Section", "IncrWave2", 2)),
            references,
            ini.GetInt("Instrument Setup Section", "RefBefore", 32),
            ini.GetInt("Instrument Setup Section", "ScanAverage", 0),
            ini.GetInt("Instrument Setup Section", "RefAfter", 32),
            ini.GetInt("Instrument Setup Section", "Port", 0),
            ini.GetInt("Instrument Setup Section", "Baud", 3),
            ini.GetDouble("Instrument Setup Section", "NIRK", 0),
            ini.GetDouble("Instrument Setup Section", "NIRPHI", 0),
            ini.GetDouble("Instrument Setup Section", "VISK", 0),
            ini.GetDouble("Instrument Setup Section", "VISPHI", 0));
    }
}
