using Avalonia.Controls;
using Avalonia.Interactivity;
using Nir5000.Core;

namespace Nir5000.App;

public sealed partial class MainWindow : Window
{
    private Nir5000Profile? profile;
    private Nir5000Project? project;
    private Nir5000SerialSession? serial;
    private readonly SimulatedNir5000Device simulatedDevice = new();

    public MainWindow() => InitializeComponent();

    private void LoadProfile(object? sender, RoutedEventArgs e)
    {
        try
        {
            project = Nir5000Project.Load(ProfilePath.Text ?? string.Empty);
            profile = project.Profile;
            var count = profile.ScanRange.Wavelengths().Count;
            var baudText = Nir5000BaudRates.TryResolve(profile.BaudRateCode, out var baud) ? baud.ToString() : "unknown";
            if (baudText != "unknown") SerialBaudPath.Text = baudText;
            ProfileSummary.Text = $"Instrument type {profile.InstrumentType} | {project.Products.Count} products | range {profile.ScanRange.StartNm:F0}-{profile.ScanRange.EndNm:F0} nm | {count} points | baud code {profile.BaudRateCode} ({baudText})";
            Status.Text = "Profile loaded.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Profile error: {ex.Message}";
        }
    }

    private async void AcquireSimulation(object? sender, RoutedEventArgs e)
    {
        if (profile is null) LoadProfile(sender, e);
        if (profile is null) return;
        await simulatedDevice.InitializeAsync();
        var spectrum = await simulatedDevice.AcquireSpectrumAsync(profile.ScanRange.Wavelengths());
        var preview = spectrum.WavelengthsNm.Zip(spectrum.Values).Take(24).Select(p => $"{p.First,8:F1} nm  {p.Second,12:F8}");
        SpectrumPreview.Text = string.Join(Environment.NewLine, preview);
        Status.Text = $"Offline simulator: {simulatedDevice.Identity.Model}; spectrum acquired at {spectrum.CapturedAt.LocalDateTime:G}.";
    }

    private void LoadNir(object? sender, RoutedEventArgs e)
    {
        try
        {
            var range = profile?.ScanRange ?? new ScanRange(1100, 2498, 2);
            var file = LegacyNirFile.Read(NirPath.Text ?? string.Empty, range);
            ProfileSummary.Text = $"{file.Metadata.InstrumentName} | serial {file.Metadata.SerialNumber} | {file.Metadata.PointCount} points";
            var preview = file.Spectrum.WavelengthsNm.Zip(file.Spectrum.Values).Take(24).Select(p => $"{p.First,8:F1} nm  {p.Second,12:F8}");
            SpectrumPreview.Text = string.Join(Environment.NewLine, preview);
            Status.Text = "Legacy NIR spectrum loaded.";
        }
        catch (Exception ex)
        {
            Status.Text = $"NIR file error: {ex.Message}";
        }
    }

    private void ToggleSerial(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (serial is not null)
            {
                serial.Dispose();
                serial = null;
                SerialStatus.Text = "Serial connection closed.";
                ((Avalonia.Controls.Button)sender!).Content = "Open serial";
                return;
            }

            if (!int.TryParse(SerialBaudPath.Text, out var baud) || baud <= 0)
                throw new InvalidOperationException("Enter a positive numeric baud rate.");
            serial = new Nir5000SerialSession(new Nir5000SerialOptions(SerialPortPath.Text ?? string.Empty, baud));
            serial.Open();
            SerialStatus.Text = $"Connected to {SerialPortPath.Text} at {baud} baud.";
            ((Avalonia.Controls.Button)sender!).Content = "Close serial";
        }
        catch (Exception ex)
        {
            serial?.Dispose();
            serial = null;
            SerialStatus.Text = $"Serial error: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        serial?.Dispose();
        base.OnClosed(e);
    }
}
