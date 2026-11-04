using Nir5000.Core;

if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
{
    var header = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    var payload = new byte[] { 0x10, 0x20, 0x30 };
    var encoded = Nir5000FrameCodec.BuildSendFrame(header, payload);
    var decoded = Nir5000FrameCodec.ParseReceivedFrame(encoded, payload.Length);
    if (!decoded.Header.SequenceEqual(header) || !decoded.Payload.SequenceEqual(payload) || !decoded.HasValidChecksum)
        throw new InvalidOperationException("NIR 5000 frame codec self-test failed.");
    if (Nir5000Commands.GetSerialNumber != 0x5B || Nir5000Commands.GetSpectrum != 0x5D || Nir5000Commands.MaximumSegmentLength != 0xF0)
        throw new InvalidOperationException("NIR 5000 command constants self-test failed.");
    if (!Nir5000BaudRates.TryResolve(5, out var legacyBaud) || legacyBaud != 38400)
        throw new InvalidOperationException("NIR 5000 baud-rate mapping self-test failed.");
    var simulatedDevice = new SimulatedNir5000Device();
    try
    {
        await simulatedDevice.AcquireSpectrumAsync(new[] { 1100d });
        throw new InvalidOperationException("NIR 5000 simulator initialization guard failed.");
    }
    catch (InvalidOperationException)
    {
        await simulatedDevice.InitializeAsync();
        var simulatedSpectrum = await simulatedDevice.AcquireSpectrumAsync(new[] { 1100d, 1102d });
        if (!simulatedDevice.IsInitialized || simulatedSpectrum.Values.Count != 2 || simulatedDevice.Identity.SerialNumber != "SIM-5000")
            throw new InvalidOperationException("NIR 5000 simulator self-test failed.");
    }
    var preamble = Nir5000FrameCodec.BuildSendPreamble();
    if (!preamble.SequenceEqual(new byte[] { Nir5000ProtocolConstants.Enquiry }))
        throw new InvalidOperationException("NIR 5000 transaction preamble self-test failed.");
    var transferData = Enumerable.Range(0, Nir5000Commands.MaximumSegmentLength * 2 + 3)
        .Select(value => (byte)(value % 256)).ToArray();
    var segments = Nir5000Transfer.Segment(transferData);
    if (segments.Count != 3 || segments[0].Offset != 0 || segments[0].Length != 0xF0 ||
        segments[1].Offset != 0xF0 || segments[1].Length != 0xF0 ||
        segments[2].Offset != 0x1E0 || segments[2].Length != 3 ||
        !segments.SelectMany(segment => segment.Payload).SequenceEqual(transferData))
        throw new InvalidOperationException("NIR 5000 transfer segmentation self-test failed.");
    using var fake = new LoopbackTransport();
    var session = new Nir5000ProtocolSession(fake);
    session.Open();
    if (!session.IsOpen) throw new InvalidOperationException("NIR 5000 protocol open-state self-test failed.");
    session.SendFrame(header, payload);
    var expectedWrites = Nir5000FrameCodec.BuildSendPreamble().Concat(encoded);
    if (!fake.Writes.SelectMany(bytes => bytes).SequenceEqual(expectedWrites))
        throw new InvalidOperationException("NIR 5000 serial-layer self-test failed.");
    session.Dispose();
    if (session.IsOpen) throw new InvalidOperationException("NIR 5000 protocol disposal self-test failed.");
    using var receiveFake = new LoopbackTransport(new[] { Nir5000ProtocolConstants.Enquiry }.Concat(encoded));
    var received = new Nir5000ProtocolSession(receiveFake);
    received.Open();
    var receivedFrame = received.ReceiveFrame(payload.Length);
    if (!receivedFrame.Payload.SequenceEqual(payload) ||
        !receiveFake.Writes.SelectMany(bytes => bytes).SequenceEqual(new[] { Nir5000ProtocolConstants.Ack, Nir5000ProtocolConstants.Ack }))
        throw new InvalidOperationException("NIR 5000 receive-layer self-test failed.");
    var invalidFrameBytes = encoded.ToArray();
    invalidFrameBytes[^1]++;
    using var invalidFake = new LoopbackTransport(new[] { Nir5000ProtocolConstants.Enquiry }.Concat(invalidFrameBytes));
    var invalidSession = new Nir5000ProtocolSession(invalidFake);
    invalidSession.Open();
    try
    {
        _ = invalidSession.ReceiveFrame(payload.Length);
        throw new InvalidOperationException("NIR 5000 invalid-frame self-test did not fail.");
    }
    catch (InvalidDataException)
    {
        if (!invalidFake.Writes.SelectMany(bytes => bytes).SequenceEqual(new[] { Nir5000ProtocolConstants.Ack, Nir5000ProtocolConstants.Nak }))
            throw new InvalidOperationException("NIR 5000 NAK self-test failed.");
    }
    var capture = new byte[] { 0x55, 0x04 }.Concat(encoded).Concat(new byte[] { 0x06, 0x7F }).ToArray();
    var captured = Nir5000Capture.FindFrames(capture, payload.Length);
    if (captured.Count != 1 || captured[0].Offset != 2 || !captured[0].Frame.Payload.SequenceEqual(payload))
        throw new InvalidOperationException("NIR 5000 capture parser self-test failed.");
    var headerBytes = new byte[Nir5000CalibrationTable.StepOffset + Nir5000CalibrationTable.EntryStride + sizeof(float)];
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(headerBytes, 1);
    WriteHeaderFloat(headerBytes, Nir5000CalibrationTable.TableAOffset, 1.25f);
    WriteHeaderFloat(headerBytes, Nir5000CalibrationTable.TableBOffset, 2.5f);
    WriteHeaderFloat(headerBytes, Nir5000CalibrationTable.StepOffset, 4f);
    var calibrationTable = Nir5000CalibrationTable.Parse(headerBytes);
    if (calibrationTable.Blocks.Count != 1 || calibrationTable.Blocks[0] != new Nir5000CalibrationBlock(1.25f, 2.5f, 4f))
        throw new InvalidOperationException("NIR 5000 calibration-table parser self-test failed.");
    var fixtureRoot = Path.Combine(Path.GetTempPath(), "nir5000-self-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(fixtureRoot);
    try
    {
        var fixtureNir = Path.Combine(fixtureRoot, "fixture.nir");
        var roundTripNir = Path.Combine(fixtureRoot, "roundtrip.nir");
        var fixtureBytes = new byte[LegacyNirFile.SpectrumOffset + 3 * sizeof(float)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(fixtureBytes.AsSpan(2, 2), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(fixtureBytes.AsSpan(6, 2), 3);
        WriteFixedAscii(fixtureBytes, 0x12, 80, "offline regression fixture");
        WriteFixedAscii(fixtureBytes, 0x52, 16, "SIM-5000");
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(fixtureBytes.AsSpan(0x80, 2), 11);
        WriteFixedAscii(fixtureBytes, 0x82, 32, "NIRSystem 5000");
        var fixtureValues = new[] { 1.25d, 2.5d, 3.75d };
        for (var index = 0; index < fixtureValues.Length; index++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                fixtureBytes.AsSpan(LegacyNirFile.SpectrumOffset + index * sizeof(float), sizeof(float)),
                BitConverter.SingleToInt32Bits((float)fixtureValues[index]));
        File.WriteAllBytes(fixtureNir, fixtureBytes);
        var fixture = LegacyNirFile.Read(fixtureNir, new ScanRange(1100, 1104, 2));
        fixture.WriteSingleSpectrum(roundTripNir, fixture.Spectrum);
        if (!fixture.Spectrum.Values.SequenceEqual(fixtureValues) ||
            !File.ReadAllBytes(fixtureNir).SequenceEqual(File.ReadAllBytes(roundTripNir)))
            throw new InvalidOperationException("NIR 5000 legacy-file roundtrip self-test failed.");

        var projectPath = Path.Combine(fixtureRoot, "Prod5000.ini");
        File.WriteAllText(projectPath, """
[Stats]
NumProducts=1
[0]
InstrumentType=11
ProductCode=42
Description=Regression product
Check cell=1
[ScanRange0]
Starting0=1100
Ending0=1104
Increment0=2
""");
        var project = Nir5000Project.Load(projectPath);
        if (project.Profile.InstrumentType != 11 || project.Products.Count != 1 ||
            project.Products[0].ProductCode != 42 || !project.Products[0].IsCheckCell ||
            project.Products[0].ScanRange.Wavelengths().Count != 3)
            throw new InvalidOperationException("NIR 5000 project-file self-test failed.");
    }
    finally
    {
        if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
    }
    Console.WriteLine("NIR 5000 codec, transport and command self-tests passed.");
    return 0;
}

if (args.Length >= 3 && args[0].Equals("--inspect-nir", StringComparison.OrdinalIgnoreCase))
{
    var range = args.Length == 3
        ? new ScanRange(1100, 2498, 2)
        : new ScanRange(double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
    var nirOutput = args.Length == 3 ? args[2] : args[5];
    var file = LegacyNirFile.Read(args[1], range);
    Console.WriteLine($"{file.Metadata.InstrumentName};{file.Metadata.SerialNumber};{file.Metadata.PointCount};{file.Spectrum.Values[0]:G9}");
    file.WriteSingleSpectrum(nirOutput, file.Spectrum);
    return 0;
}

if (args.Length == 2 && args[0].Equals("--inspect-project", StringComparison.OrdinalIgnoreCase))
{
    var project = Nir5000Project.Load(args[1]);
    Console.WriteLine($"Instrument type {project.Profile.InstrumentType}; {project.Products.Count} products");
    foreach (var product in project.Products)
        Console.WriteLine($"{product.Index}: code {product.ProductCode}; {product.Description}; {product.ScanRange.StartNm:G}-{product.ScanRange.EndNm:G} nm/{product.ScanRange.IncrementNm:G}");
    return 0;
}

if (args.Length == 3 && args[0].Equals("--inspect-capture", StringComparison.OrdinalIgnoreCase))
{
    var capture = File.ReadAllBytes(args[1]);
    var payloadLength = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    var frames = Nir5000Capture.FindFrames(capture, payloadLength);
    Console.WriteLine($"Capture bytes: {capture.Length}; valid frames: {frames.Count}");
    foreach (var item in frames)
        Console.WriteLine($"offset={item.Offset}; header={Convert.ToHexString(item.Frame.Header)}; payload={Convert.ToHexString(item.Frame.Payload)}");
    return 0;
}

if (args.Length == 2 && args[0].Equals("--inspect-calibration", StringComparison.OrdinalIgnoreCase))
{
    var table = Nir5000CalibrationTable.Parse(File.ReadAllBytes(args[1]));
    Console.WriteLine($"Calibration blocks: {table.Blocks.Count}");
    for (var index = 0; index < table.Blocks.Count; index++)
    {
        var block = table.Blocks[index];
        Console.WriteLine($"{index}: A={block.TableA:G9}; B={block.TableB:G9}; step={block.Step:G9}");
    }
    return 0;
}

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Nir5000.Cli <Prod5000.ini> [output.csv] | --inspect-project <Prod5000.ini> | --inspect-nir ... | --inspect-capture <capture.bin> <payloadLength> | --inspect-calibration <buffer.bin>");
    return 2;
}

var profile = Nir5000Profile.Load(args[0]);
var wavelengths = profile.ScanRange.Wavelengths();
var spectrum = await new SimulatedSpectrumSource().AcquireAsync(wavelengths);
var output = args.Length > 1 ? args[1] : "spectrum.csv";
spectrum.SaveCsv(output);
Console.WriteLine($"Profile: instrument type {profile.InstrumentType}, {wavelengths.Count} wavelengths");
Console.WriteLine($"Simulation spectrum written to {Path.GetFullPath(output)}");
return 0;

static void WriteHeaderFloat(Span<byte> destination, int offset, float value) =>
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(float)), BitConverter.SingleToInt32Bits(value));

static void WriteFixedAscii(Span<byte> destination, int offset, int length, string value)
{
    var bytes = System.Text.Encoding.ASCII.GetBytes(value);
    bytes.AsSpan(0, Math.Min(bytes.Length, length)).CopyTo(destination.Slice(offset, length));
}

sealed class LoopbackTransport : INir5000ByteTransport
{
    private readonly Queue<byte> incoming;
    public LoopbackTransport() : this(new[] { Nir5000ProtocolConstants.Ack, Nir5000ProtocolConstants.Ack }) { }
    public LoopbackTransport(IEnumerable<byte> incomingBytes) => incoming = new(incomingBytes);
    public List<byte[]> Writes { get; } = new();
    public bool IsOpen { get; private set; }
    public void Open() => IsOpen = true;
    public int ReadByte() => incoming.Count == 0 ? throw new TimeoutException("Loopback response queue is empty.") : incoming.Dequeue();
    public void Write(ReadOnlySpan<byte> data) => Writes.Add(data.ToArray());
    public void Dispose() => IsOpen = false;
}
