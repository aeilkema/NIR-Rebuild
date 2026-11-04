using System.Text.Json;

namespace Nir5000.App;

public sealed class AppSettings
{
    public string ProfilePath { get; set; } = string.Empty;
    public string SerialPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 38400;
    public bool AutoConnect { get; set; }
    public string LogDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nir5000Rebuild", "logs");
    public string TransmitCapturePath { get; set; } = string.Empty;
    public string ReceiveCapturePath { get; set; } = string.Empty;
    public string LimsOrderNumber { get; set; } = string.Empty;
    public string LimsCalibration { get; set; } = string.Empty;
    public string LimsOutputPath { get; set; } = string.Empty;
    public string NovaBaseUri { get; set; } = "http://localhost:8080/api";
    public int NovaInstrumentTypeId { get; set; }
    public long NovaChassisId { get; set; }
    public string NovaSampleNumber { get; set; } = "NIR-Rebuild-replay";
    public string NovaProductName { get; set; } = "NIR 5000 replay";
    public string NovaProductCode { get; set; } = "NIR5000";

    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nir5000Rebuild", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class AppFileLogger : IDisposable
{
    private readonly StreamWriter writer;
    private readonly object sync = new();

    public AppFileLogger(string directory)
    {
        Directory.CreateDirectory(directory);
        writer = new StreamWriter(Path.Combine(directory, $"nir5000-{DateTime.Now:yyyyMMdd-HHmmss}.log"), append: true) { AutoFlush = true };
        Info("Application started.");
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception exception) => Write("ERROR", $"{message}: {exception}");

    private void Write(string level, string message)
    {
        lock (sync) writer.WriteLine($"{DateTimeOffset.Now:O} [{level}] {message}");
    }

    public void Dispose() => writer.Dispose();
}
