namespace Nir5000.Core;

public sealed record Spectrum(DateTimeOffset CapturedAt, IReadOnlyList<double> WavelengthsNm, IReadOnlyList<double> Values)
{
    public void SaveCsv(string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("wavelength_nm,value");
        for (var i = 0; i < WavelengthsNm.Count; i++) writer.WriteLine($"{WavelengthsNm[i]:F3},{Values[i]:G17}");
    }
}
