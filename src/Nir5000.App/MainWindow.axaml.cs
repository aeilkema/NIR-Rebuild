using Avalonia.Controls;
using Avalonia.Interactivity;
using Nir5000.Core;
using System.Net;

namespace Nir5000.App;

public sealed partial class MainWindow : Window
{
    private Nir5000Profile? profile;
    private Nir5000Project? project;
    private Nir5000SerialSession? serial;
    private SerialTcpBridge? bridge;
    private readonly SimulatedNir5000Device simulatedDevice = new();
    private Spectrum? currentSpectrum;
    private readonly AppSettings settings;
    private readonly AppFileLogger logger;

    public MainWindow()
    {
        settings = AppSettings.Load();
        logger = new AppFileLogger(settings.LogDirectory);
        InitializeComponent();
        ProfilePath.Text = settings.ProfilePath;
        SerialPortPath.Text = settings.SerialPort;
        SerialBaudPath.Text = settings.BaudRate.ToString();
        AutoConnectPath.IsChecked = settings.AutoConnect;
        AutoStartBridgePath.IsChecked = settings.AutoStartBridge;
        BridgeListenAddressPath.Text = settings.BridgeListenAddress;
        BridgeTcpPortPath.Text = settings.BridgeTcpPort.ToString();
        LogDirectoryPath.Text = settings.LogDirectory;
        TransmitCapturePath.Text = settings.TransmitCapturePath;
        ReceiveCapturePath.Text = settings.ReceiveCapturePath;
        WireTracePath.Text = settings.WireTracePath;
        LimsOrderNumberPath.Text = settings.LimsOrderNumber;
        LimsCalibrationPath.Text = settings.LimsCalibration;
        LimsOutputPath.Text = settings.LimsOutputPath;
        NovaBaseUriPath.Text = settings.NovaBaseUri;
        NovaInstrumentTypePath.Text = settings.NovaInstrumentTypeId.ToString();
        NovaChassisPath.Text = settings.NovaChassisId.ToString();
        NovaSampleNumberPath.Text = settings.NovaSampleNumber;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        logger.Info($"Window opened; autoConnect={settings.AutoConnect}, port={settings.SerialPort}, baud={settings.BaudRate}.");
        if (settings.AutoStartBridge) ToggleBridge(null, null);
        if (settings.AutoConnect && !settings.AutoStartBridge) ToggleSerial(null, null);
        if (settings.AutoConnect && settings.AutoStartBridge)
            logger.Info("Both auto-connect and auto-start bridge were configured; bridge startup took precedence to avoid opening the COM port twice.");
    }

    private void SaveSettings(object? sender, RoutedEventArgs? e)
    {
        settings.ProfilePath = ProfilePath.Text ?? string.Empty;
        settings.SerialPort = SerialPortPath.Text ?? "COM1";
        settings.BaudRate = int.TryParse(SerialBaudPath.Text, out var baud) ? baud : 38400;
        settings.AutoConnect = AutoConnectPath.IsChecked == true;
        settings.AutoStartBridge = AutoStartBridgePath.IsChecked == true;
        settings.BridgeListenAddress = BridgeListenAddressPath.Text ?? "127.0.0.1";
        settings.BridgeTcpPort = int.TryParse(BridgeTcpPortPath.Text, out var tcpPort) ? tcpPort : 9000;
        settings.LogDirectory = LogDirectoryPath.Text ?? settings.LogDirectory;
        settings.TransmitCapturePath = TransmitCapturePath.Text ?? string.Empty;
        settings.ReceiveCapturePath = ReceiveCapturePath.Text ?? string.Empty;
        settings.WireTracePath = WireTracePath.Text ?? string.Empty;
        settings.LimsOrderNumber = LimsOrderNumberPath.Text ?? string.Empty;
        settings.LimsCalibration = LimsCalibrationPath.Text ?? string.Empty;
        settings.LimsOutputPath = LimsOutputPath.Text ?? string.Empty;
        settings.NovaBaseUri = NovaBaseUriPath.Text ?? string.Empty;
        settings.NovaInstrumentTypeId = int.TryParse(NovaInstrumentTypePath.Text, out var typeId) ? typeId : 0;
        settings.NovaChassisId = long.TryParse(NovaChassisPath.Text, out var chassisId) ? chassisId : 0;
        settings.NovaSampleNumber = NovaSampleNumberPath.Text ?? string.Empty;
        settings.Save();
        logger.Info("Settings saved.");
        Status.Text = $"Settings saved to {AppSettings.FilePath}.";
    }

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
            logger.Info($"Profile loaded: {ProfilePath.Text}.");
        }
        catch (Exception ex)
        {
            Status.Text = $"Profile error: {ex.Message}";
            logger.Error("Profile load failed", ex);
        }
    }

    private async void AcquireSimulation(object? sender, RoutedEventArgs e)
    {
        if (profile is null) LoadProfile(sender, e);
        if (profile is null) return;
        await simulatedDevice.InitializeAsync();
        var spectrum = await simulatedDevice.AcquireSpectrumAsync(profile.ScanRange.Wavelengths());
        currentSpectrum = spectrum;
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
            currentSpectrum = file.Spectrum;
        }
        catch (Exception ex)
        {
            Status.Text = $"NIR file error: {ex.Message}";
        }
    }

    private void PredictEqa(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (currentSpectrum is null)
                throw new InvalidOperationException("Load a legacy NIR file or run a simulated scan first.");
            var model = EqaModel.LoadJson(EqaModelPath.Text ?? string.Empty);
            var predictions = EqaPredictionEngine.Predict(model, currentSpectrum);
            SpectrumPreview.Text = string.Join(Environment.NewLine, predictions.Select(prediction =>
                $"{prediction.Parameter,-16} raw={prediction.RawValue,12:G8} value={prediction.Value,12:G8}"));
            Status.Text = $"EQA prediction complete: {model.Name}; {predictions.Count} parameter(s).";
        }
        catch (Exception ex)
        {
            Status.Text = $"EQA error: {ex.Message}";
        }
    }

    private void PredictLegacyEqa(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (currentSpectrum is null)
                throw new InvalidOperationException("Load a legacy NIR file or run a simulated scan first.");
            var model = LegacyEqaModel.ReadNir5000(EqaModelPath.Text ?? string.Empty);
            if (currentSpectrum.Values.Count != model.Container.SourcePointCount)
                throw new InvalidOperationException($"Spectrum has {currentSpectrum.Values.Count} points; model requires {model.Container.SourcePointCount}.");
            SpectrumPreview.Text = string.Join(Environment.NewLine, model.Parameters.Select(parameter =>
                $"{parameter.Name,-16} prediction={parameter.PredictSpectrum(currentSpectrum.Values.ToArray()),12:G8} mean={parameter.EquationMean,10:G8} sec={parameter.EquationSec,10:G8} rsq={parameter.EquationRsq,10:G8}"));
            Status.Text = $"Legacy EQA prediction complete: {model.Parameters.Count} parameter(s); replay/simulator provenance.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Legacy EQA error: {ex.Message}";
        }
    }

    private void ExportLims(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (currentSpectrum is null) throw new InvalidOperationException("Load a legacy NIR file or run a simulated scan first.");
            var model = LegacyEqaModel.ReadNir5000(EqaModelPath.Text ?? string.Empty);
            if (currentSpectrum.Values.Count != model.Container.SourcePointCount)
                throw new InvalidOperationException($"Spectrum has {currentSpectrum.Values.Count} points; model requires {model.Container.SourcePointCount}.");
            if (string.IsNullOrWhiteSpace(LimsOrderNumberPath.Text) || string.IsNullOrWhiteSpace(LimsCalibrationPath.Text) || string.IsNullOrWhiteSpace(LimsOutputPath.Text))
                throw new InvalidOperationException("Enter LIMS order, calibration code and output path.");
            var calibration = LimsCalibrationPath.Text.Split('-', 2, StringSplitOptions.TrimEntries);
            var document = new FossNirCommaSep2Document(
                LimsOrderNumberPath.Text,
                DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                "NIRSystem 5000",
                calibration[0],
                calibration.Length == 2 ? calibration[1] : string.Empty,
                model.Parameters.Select(parameter => new FossNirResultRow(parameter.Name, parameter.PredictSpectrum(currentSpectrum.Values.ToArray()))).ToArray());
            document.Write(LimsOutputPath.Text);
            SaveSettings(null, null);
            Status.Text = $"LIMS file written: {LimsOutputPath.Text}";
            logger.Info($"LIMS CommaSep2 export written: {LimsOutputPath.Text}; order={LimsOrderNumberPath.Text}.");
        }
        catch (Exception ex)
        {
            Status.Text = $"LIMS export error: {ex.Message}";
            logger.Error("LIMS export failed", ex);
        }
    }

    private void ToggleSerial(object? sender, RoutedEventArgs? e)
    {
        try
        {
            if (serial is not null)
            {
                serial.Dispose();
                serial = null;
                SerialStatus.Text = "Serial connection closed.";
                SerialToggleButton.Content = "Open serial";
                logger.Info("Serial closed by user or startup toggle.");
                return;
            }

            if (!int.TryParse(SerialBaudPath.Text, out var baud) || baud <= 0)
                throw new InvalidOperationException("Enter a positive numeric baud rate.");
            var txCapture = string.IsNullOrWhiteSpace(TransmitCapturePath.Text) ? null : TransmitCapturePath.Text;
            var rxCapture = string.IsNullOrWhiteSpace(ReceiveCapturePath.Text) ? null : ReceiveCapturePath.Text;
            var wireTrace = string.IsNullOrWhiteSpace(WireTracePath.Text) ? null : WireTracePath.Text;
            serial = new Nir5000SerialSession(new Nir5000SerialOptions(SerialPortPath.Text ?? string.Empty, baud, 1000, txCapture, rxCapture, wireTrace));
            serial.Open();
            SerialStatus.Text = $"Connected to {SerialPortPath.Text} at {baud} baud.";
            SerialToggleButton.Content = "Close serial";
            logger.Info($"Serial opened: {SerialPortPath.Text} at {baud} baud; capture={(txCapture is null ? "off" : "on")}; trace={(wireTrace is null ? "off" : wireTrace)}.");
        }
        catch (Exception ex)
        {
            serial?.Dispose();
            serial = null;
            SerialToggleButton.Content = "Open serial";
            SerialStatus.Text = $"Serial error: {ex.Message}";
            logger.Error("Serial open failed", ex);
        }
    }

    private void LampOn(object? sender, RoutedEventArgs e) => SetLamp(true);

    private void ToggleBridge(object? sender, RoutedEventArgs? e)
    {
        if (bridge is not null)
        {
            _ = StopBridgeAsync();
            return;
        }

        try
        {
            if (serial is not null)
                throw new InvalidOperationException("Close the direct serial connection before starting the Ethernet bridge.");
            if (!int.TryParse(SerialBaudPath.Text, out var baud) || baud <= 0)
                throw new InvalidOperationException("Enter a positive numeric baud rate.");
            if (!int.TryParse(BridgeTcpPortPath.Text, out var tcpPort) || tcpPort is < 1 or > 65535)
                throw new InvalidOperationException("Enter a TCP port between 1 and 65535.");
            if (!IPAddress.TryParse(BridgeListenAddressPath.Text, out var listenAddress))
                throw new InvalidOperationException("Enter a numeric listen address, for example 127.0.0.1.");

            var txCapture = string.IsNullOrWhiteSpace(TransmitCapturePath.Text) ? null : TransmitCapturePath.Text;
            var rxCapture = string.IsNullOrWhiteSpace(ReceiveCapturePath.Text) ? null : ReceiveCapturePath.Text;
            var wireTrace = string.IsNullOrWhiteSpace(WireTracePath.Text) ? null : WireTracePath.Text;
            bridge = new SerialTcpBridge(new SerialTcpBridgeOptions(
                SerialPortPath.Text ?? string.Empty, baud, tcpPort, listenAddress, 250, txCapture, rxCapture, wireTrace));
            _ = RunBridgeAsync(bridge);
            BridgeStatus.Text = $"Bridge starting on {listenAddress}:{tcpPort}.";
            BridgeToggleButton.Content = "Stop bridge";
            logger.Info($"Ethernet bridge starting: {SerialPortPath.Text} at {baud} baud -> {listenAddress}:{tcpPort}; capture={(txCapture is null ? "off" : "on")}; trace={(wireTrace ?? "off")}");
        }
        catch (Exception ex)
        {
            bridge = null;
            BridgeStatus.Text = $"Bridge error: {ex.Message}";
            logger.Error("Ethernet bridge start failed", ex);
        }
    }

    private async Task RunBridgeAsync(SerialTcpBridge activeBridge)
    {
        try
        {
            await activeBridge.RunAsync();
            logger.Info("Ethernet bridge stopped.");
        }
        catch (Exception ex)
        {
            BridgeStatus.Text = $"Bridge error: {ex.Message}";
            logger.Error("Ethernet bridge failed", ex);
        }
        finally
        {
            if (ReferenceEquals(bridge, activeBridge))
            {
                bridge = null;
                BridgeToggleButton.Content = "Start bridge";
                BridgeStatus.Text = "Bridge stopped.";
            }
        }
    }

    private async Task StopBridgeAsync()
    {
        var activeBridge = bridge;
        if (activeBridge is null) return;
        bridge = null;
        await activeBridge.DisposeAsync();
        BridgeToggleButton.Content = "Start bridge";
        BridgeStatus.Text = "Bridge stopped.";
        logger.Info("Ethernet bridge stopped by user.");
    }

    private void LampOff(object? sender, RoutedEventArgs e) => SetLamp(false);

    private void SetLamp(bool enabled)
    {
        try
        {
            if (serial is null) throw new InvalidOperationException("Open the serial connection first.");
            serial.SetLamp(enabled);
            SerialStatus.Text = $"Lamp {(enabled ? "on" : "off")} request sent (02 {(enabled ? "FF" : "00")}); legacy driver reports state locally.";
            logger.Info($"Lamp {(enabled ? "on" : "off")} request sent: {Convert.ToHexString(Nir5000LampCommands.BuildObservedRequest(enabled))}.");
        }
        catch (Exception ex)
        {
            SerialStatus.Text = $"Lamp error: {ex.Message}";
            logger.Error("Lamp command failed", ex);
        }
    }

    private async void SubmitNova(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (currentSpectrum is null) throw new InvalidOperationException("Load a legacy NIR file or run a simulated scan first.");
            if (!Uri.TryCreate(NovaBaseUriPath.Text, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException("Enter a valid Nova base URL.");
            if (!int.TryParse(NovaInstrumentTypePath.Text, out var typeId) || typeId < 0)
                throw new InvalidOperationException("Enter a non-negative Nova instrument type ID.");
            if (!long.TryParse(NovaChassisPath.Text, out var chassisId) || chassisId < 0)
                throw new InvalidOperationException("Enter a non-negative Nova chassis ID.");
            var sample = NovaOpenInstrumentExport.Create(currentSpectrum, NovaSampleNumberPath.Text ?? "NIR-Rebuild-replay");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var adapter = new NovaOpenInstrumentAdapter(client, new NovaOpenInstrumentOptions(baseUri, typeId, chassisId));
            var result = await adapter.SubmitSampleAsync(sample);
            SaveSettings(null, null);
            Status.Text = $"Nova submit {(result.IsSuccess ? "succeeded" : "failed")}: {(int)result.StatusCode} {result.StatusCode}";
            logger.Info($"Nova submit: status={(int)result.StatusCode}; endpoint={baseUri}; type={typeId}; chassis={chassisId}; sample={NovaSampleNumberPath.Text}; response={result.ResponseBody}");
        }
        catch (Exception ex)
        {
            Status.Text = $"Nova error: {ex.Message}";
            logger.Error("Nova submit failed", ex);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopBridgeAsync().GetAwaiter().GetResult();
        serial?.Dispose();
        SaveSettings(null, new RoutedEventArgs());
        logger.Info("Application closing.");
        logger.Dispose();
        base.OnClosed(e);
    }
}
