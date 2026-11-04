using Nir5000.Core;
using System.Net;

if ((args.Length is 4 or 5 or 7) && args[0].Equals("--serial-tcp-bridge", StringComparison.OrdinalIgnoreCase))
{
    var listenAddress = args.Length == 5
        ? IPAddress.Parse(args[4])
        : IPAddress.Loopback;
    var bridge = new SerialTcpBridge(new SerialTcpBridgeOptions(
        args[1],
        int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
        listenAddress,
        TransmitCapturePath: args.Length == 7 ? args[5] : null,
        ReceiveCapturePath: args.Length == 7 ? args[6] : null));
    Console.WriteLine($"Raw serial/TCP bridge listening on {listenAddress}:{args[3]} -> {args[1]} at {args[2]} baud{(args.Length == 7 ? $"; capture TX={args[5]}, RX={args[6]}" : string.Empty)}. Press Ctrl+C to stop.");
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
    await bridge.RunAsync(stop.Token);
    await bridge.DisposeAsync();
    return 0;
}

if (args.Length is 5 or 6 && args[0].Equals("--upload-starlims", StringComparison.OrdinalIgnoreCase))
{
    if (!Uri.TryCreate(args[2], UriKind.Absolute, out var endpoint))
        throw new ArgumentException("STARLIMS endpoint must be an absolute URL.");
    var method = args.Length == 6 ? args[5] : "upload";
    using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var uploader = new StarLimsUploadClient(client, new StarLimsUploadOptions(endpoint, args[3], args[4], method));
    var result = await uploader.UploadFileAsync(args[1]);
    Console.WriteLine($"STARLIMS upload {(result.IsSuccess ? "succeeded" : "failed")}: {(int)result.StatusCode} {result.StatusCode}; file={result.FileName}; response={result.ResponseBody}");
    return result.IsSuccess ? 0 : 1;
}

if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
{
    var header = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    var payload = new byte[] { 0x10, 0x20, 0x30 };
    var encoded = Nir5000FrameCodec.BuildSendFrame(header, payload);
    var decoded = Nir5000FrameCodec.ParseReceivedFrame(encoded, payload.Length);
    if (!decoded.Header.SequenceEqual(header) || !decoded.Payload.SequenceEqual(payload) || !decoded.HasValidChecksum)
        throw new InvalidOperationException("NIR 5000 frame codec self-test failed.");
    if (Nir5000Commands.GetSerialNumber != 0x5B || Nir5000Commands.GetSpectrum != 0x5D ||
        Nir5000Commands.Command5A != 0x5A || Nir5000Commands.Command5C != 0x5C || Nir5000Commands.Command5E != 0x5E ||
        Nir5000Commands.ThreeByteExchangeLength != 3 || Nir5000Commands.TwoByteExchangeLength != 2 ||
        Nir5000Commands.EightByteExchangeLength != 8 || Nir5000Commands.MaximumSegmentLength != 0xF0)
        throw new InvalidOperationException("NIR 5000 command constants self-test failed.");
    var starlimsSignature = StarLimsUploadClient.CreateSignature(
        new Uri("https://lims/rest.web.api/upload"), "POST", "ACCESS", "upload",
        "2026-01-01T00:00:00.000Z", "abc"u8, "SECRET");
    if (starlimsSignature != "rDLdIDJty34nHHMrM8Ao+DBc6LGvVplPOI0psWdF6SU=")
        throw new InvalidOperationException("STARLIMS HMAC signature self-test failed.");
    var uploadFixture = Path.Combine(Path.GetTempPath(), "nir5000-starlims-upload-test.txt");
    File.WriteAllText(uploadFixture, "abc");
    using (var uploadHandler = new CapturingStarLimsHandler())
    using (var uploadHttp = new HttpClient(uploadHandler))
    {
        var uploader = new StarLimsUploadClient(uploadHttp, new StarLimsUploadOptions(
            new Uri("https://lims/rest.web.api/upload"), "ACCESS", "SECRET"));
        var uploadResult = await uploader.UploadFileAsync(uploadFixture);
        if (!uploadResult.IsSuccess || uploadHandler.Body != "abc" ||
            uploadHandler.ContentType != "application/octet-stream" ||
            uploadHandler.FileName != "nir5000-starlims-upload-test.txt" ||
            uploadHandler.Signature != StarLimsUploadClient.CreateSignature(
                uploadHandler.RequestUri!, "POST", "ACCESS", "upload", uploadHandler.Timestamp!,
                System.Text.Encoding.UTF8.GetBytes("abc"), "SECRET"))
            throw new InvalidOperationException("STARLIMS HTTP uploader self-test failed.");
    }
    File.Delete(uploadFixture);
    if (!Nir5000LampCommands.BuildObservedRequest(true).SequenceEqual(new byte[] { 0x02, 0xFF }) ||
        !Nir5000LampCommands.BuildObservedRequest(false).SequenceEqual(new byte[] { 0x02, 0x00 }))
        throw new InvalidOperationException("NIR 5000 lamp request self-test failed.");
    using var lampFake = new LoopbackTransport();
    var lampSession = new Nir5000ProtocolSession(lampFake);
    lampSession.Open();
    lampSession.SetLamp(true);
    lampSession.SetLamp(false);
    if (!lampFake.Writes.SelectMany(bytes => bytes).SequenceEqual(new byte[] { 0x02, 0xFF, 0x02, 0x00 }))
        throw new InvalidOperationException("NIR 5000 lamp transport self-test failed.");
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
        var referenceSpectrum = await simulatedDevice.AcquireReferenceAsync(new[] { 1100d });
        if (referenceSpectrum.Values.Count != 1)
            throw new InvalidOperationException("NIR 5000 instrument contract self-test failed.");
        await simulatedDevice.TerminateAsync();
        if (simulatedDevice.IsInitialized)
            throw new InvalidOperationException("NIR 5000 simulator termination self-test failed.");
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
    var transmitTrace = new RecordingWire();
    var session = new Nir5000ProtocolSession(fake, transmitTrace);
    session.Open();
    if (!session.IsOpen) throw new InvalidOperationException("NIR 5000 protocol open-state self-test failed.");
    session.SendFrame(header, payload);
    var expectedWrites = Nir5000FrameCodec.BuildSendPreamble().Concat(encoded);
    if (!fake.Writes.SelectMany(bytes => bytes).SequenceEqual(expectedWrites))
        throw new InvalidOperationException("NIR 5000 serial-layer self-test failed.");
    if (!transmitTrace.Transmit.SequenceEqual(expectedWrites))
        throw new InvalidOperationException("NIR 5000 wire-capture transmit self-test failed.");
    session.Dispose();
    if (session.IsOpen) throw new InvalidOperationException("NIR 5000 protocol disposal self-test failed.");
    using var receiveFake = new LoopbackTransport(new[] { Nir5000ProtocolConstants.Enquiry }.Concat(encoded));
    var receiveTrace = new RecordingWire();
    var received = new Nir5000ProtocolSession(receiveFake, receiveTrace);
    received.Open();
    var receivedFrame = received.ReceiveFrame(payload.Length);
    if (!receivedFrame.Payload.SequenceEqual(payload) ||
        !receiveFake.Writes.SelectMany(bytes => bytes).SequenceEqual(new[] { Nir5000ProtocolConstants.Ack, Nir5000ProtocolConstants.Ack }))
        throw new InvalidOperationException("NIR 5000 receive-layer self-test failed.");
    var expectedReceiveTrace = new[] { Nir5000ProtocolConstants.Enquiry }.Concat(encoded).ToArray();
    if (!receiveTrace.Receive.SequenceEqual(expectedReceiveTrace))
        throw new InvalidOperationException($"NIR 5000 wire-capture receive self-test failed: actual={Convert.ToHexString(receiveTrace.Receive.ToArray())}; expected={Convert.ToHexString(expectedReceiveTrace)}.");
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

        var eqaModel = new EqaModel("self-test", "NIRSystem 5000", 3, 1100, 2,
            [new EqaParameter("PROTEIN", "X1 + 2 * X2 - X3", 1.5, 0.25)]);
        var eqaPredictions = EqaPredictionEngine.Predict(eqaModel,
            new Spectrum(DateTimeOffset.UtcNow, [1100, 1102, 1104], [1, 2, 3]));
        if (eqaPredictions.Count != 1 || Math.Abs(eqaPredictions[0].Value - 3.25) > 0.000001)
            throw new InvalidOperationException("EQA prediction self-test failed.");
        if (Math.Abs(EqaExpression.Parse("max(X1, X2) + sqrt(9)").Evaluate(
                new Dictionary<string, double> { ["X1"] = 1, ["X2"] = 2 }) - 5) > 0.000001)
            throw new InvalidOperationException("EQA expression self-test failed.");
        if (Math.Abs(EqaExpression.Parse("if(X1 >= 2 && X2 < 4, X1^2, 0)").Evaluate(
                new Dictionary<string, double> { ["X1"] = 2, ["X2"] = 3 }) - 4) > 0.000001)
            throw new InvalidOperationException("EQA conditional-expression self-test failed.");

        var eqaFixturePath = Path.Combine(fixtureRoot, "fixture.eqa");
        var eqaFixtureBytes = new byte[0x180 + 0x5F8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(eqaFixtureBytes.AsSpan(0, 2), 9);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(eqaFixtureBytes.AsSpan(2, 2), 4);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(eqaFixtureBytes.AsSpan(6, 2), 700);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(eqaFixtureBytes.AsSpan(0xA4, 2), 700);
        WriteHeaderFloat(eqaFixtureBytes, 0xCC, 1100);
        WriteHeaderFloat(eqaFixtureBytes, 0xE8, 2);
        WriteFixedAscii(eqaFixtureBytes, 0x12, 64, "EQA regression fixture");
        WriteFixedAscii(eqaFixtureBytes, 0x59, 16, "00001272");
        WriteFixedAscii(eqaFixtureBytes, 0x82, 31, "NIRSystem 5000");
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x192, 2), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x194, 2), 1);
        WriteHeaderFloat(eqaFixtureBytes, 0x19A, 1.5f);
        WriteHeaderFloat(eqaFixtureBytes, 0x19E, 12.0f);
        WriteHeaderFloat(eqaFixtureBytes, 0x1A2, 0.3f);
        WriteHeaderFloat(eqaFixtureBytes, 0x1A6, 0.95f);
        WriteFixedAscii(eqaFixtureBytes, 0x180, 16, "PROTEIN");
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x200, 2), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x202, 2), 4);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x204, 2), 4);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(eqaFixtureBytes.AsSpan(0x206, 2), 1);
        eqaFixtureBytes[0x208] = 1;
        eqaFixtureBytes[0x209] = 1;
        WriteHeaderFloat(eqaFixtureBytes, 0x4C4, 2.0f);
        WriteHeaderFloat(eqaFixtureBytes, 0x4C8, 3.0f);
        File.WriteAllBytes(eqaFixturePath, eqaFixtureBytes);
        var eqaFixture = LegacyEqaFile.Read(eqaFixturePath);
        if (eqaFixture.ModelPointCount != 700 || eqaFixture.InstrumentName != "NIRSystem 5000" ||
            eqaFixture.SerialNumber != "00001272" || !eqaFixture.CandidateNames.Contains("PROTEIN"))
            throw new InvalidOperationException("Legacy EQA header self-test failed.");
        var legacyModel = LegacyEqaModel.ReadNir5000(eqaFixturePath);
        var legacyParameter = legacyModel.Parameters.Single();
        var standardizedFixture = new double[700];
        standardizedFixture[0] = 4;
        standardizedFixture[1] = 5;
        if (legacyParameter.TermCount != 2 || legacyParameter.SelectionMask.Count(value => value != 0) != 2 ||
            legacyParameter.Preprocessing != new LegacyEqaPreprocessing(1, 4, 4, 1) ||
            Math.Abs(legacyParameter.PredictSelected([4, 5]) - 24.5) > 0.0001 ||
            Math.Abs(legacyParameter.PredictStandardized(standardizedFixture) - 24.5) > 0.0001)
            throw new InvalidOperationException("Legacy EQA coefficient self-test failed.");
        var novaSpectrum = new Spectrum(DateTimeOffset.UtcNow, [1100, 1102, 1104], [1, 2, 3]);
        var novaExport = NovaOpenInstrumentExport.Create(novaSpectrum);
        var novaSegment = novaExport.AbsScans.Single().Segments.Single();
        if (novaSegment.Unit != "nm" || novaSegment.Values.Count != 3 || novaSegment.Step != 2)
            throw new InvalidOperationException("Nova OpenInstrument export self-test failed.");
        using var novaHandler = new CapturingNovaHandler();
        using var novaHttp = new HttpClient(novaHandler);
        var novaAdapter = new NovaOpenInstrumentAdapter(novaHttp,
            new NovaOpenInstrumentOptions(new Uri("http://nova.test/api/"), 12, 345));
        var novaSubmit = await novaAdapter.SubmitSampleAsync(novaExport);
        if (!novaSubmit.IsSuccess || novaHandler.RequestUri?.AbsolutePath != "/api/instrumentTypes/12/instruments/345/samples" ||
            novaHandler.RequestBody is null || !novaHandler.RequestBody.Contains("absScans", StringComparison.Ordinal))
            throw new InvalidOperationException("Nova OpenInstrument adapter self-test failed.");
        var limsFixture = new FossNirCommaSep2Document("ORD-1", "2026-01-01", "NIRSystem 5000", "cmr05", "1",
            new[] { new FossNirResultRow("Eiwit", 31.2, 1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8, 9.9, 10.1, 0, 0, 1) });
        var limsText = limsFixture.ToText();
        if (!limsText.StartsWith("HDR;ORD-1;2026-01-01;NIRSystem 5000;cmr05-1\r\n", StringComparison.Ordinal) ||
            !limsText.Contains("PS;Eiwit;31.2;1.1;2.2;3.3;4.4;5.5;6.6;7.7;8.8;9.9;10.1\r\n", StringComparison.Ordinal) ||
            !limsText.Contains("SE;Eiwit;0;0;1;0;0;0;0\r\n", StringComparison.Ordinal))
            throw new InvalidOperationException($"FossNirCommaSep2 export self-test failed: {limsText}");
    }
    finally
    {
        if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
    }
    Console.WriteLine("NIR 5000 codec, transport and command self-tests passed.");
    return 0;
}

if (args.Length == 3 && args[0].Equals("--inspect-eqa", StringComparison.OrdinalIgnoreCase))
{
    var model = EqaModel.LoadJson(args[1]);
    var eqaSpectrum = LoadSpectrumCsv(args[2]);
    var predictions = EqaPredictionEngine.Predict(model, eqaSpectrum);
    Console.WriteLine($"Model: {model.Name}; instrument: {model.InstrumentModel}; parameters: {predictions.Count}");
    foreach (var prediction in predictions)
        Console.WriteLine($"{prediction.Parameter};raw={prediction.RawValue:G9};value={prediction.Value:G9};mean={prediction.EquationMean?.ToString("G9", System.Globalization.CultureInfo.InvariantCulture) ?? ""};sec={prediction.EquationSec?.ToString("G9", System.Globalization.CultureInfo.InvariantCulture) ?? ""};rsq={prediction.EquationRsq?.ToString("G9", System.Globalization.CultureInfo.InvariantCulture) ?? ""}");
    return 0;
}

if (args.Length == 2 && args[0].Equals("--inspect-legacy-eqa", StringComparison.OrdinalIgnoreCase))
{
    var eqa = LegacyEqaFile.Read(args[1]);
    Console.WriteLine($"EQA format={eqa.FormatVersion}; header={eqa.HeaderType}; sourcePoints={eqa.SourcePointCount}; modelPoints={eqa.ModelPointCount}");
    Console.WriteLine($"Instrument={eqa.InstrumentName}; serial={eqa.SerialNumber}; range={eqa.StartWavelengthNm:G}-{eqa.StopWavelengthNm:G} nm; step={eqa.IncrementWavelengthNm:G}");
    Console.WriteLine($"Description={eqa.Description}; recordLayout={eqa.RecordLayout}; constituentNames={string.Join(", ", eqa.CandidateNames)}");
    return 0;
}

if (args.Length == 2 && args[0].Equals("--inspect-legacy-eqa-model", StringComparison.OrdinalIgnoreCase))
{
    var model = LegacyEqaModel.ReadNir5000(args[1]);
    Console.WriteLine($"Legacy NIR5000 model: {model.Parameters.Count} parameters; recordLayout={model.Container.RecordLayout}");
    foreach (var parameter in model.Parameters)
    {
        var selected = parameter.SelectionMask.Count(value => value != 0);
        var preprocessing = parameter.Preprocessing;
        Console.WriteLine($"{parameter.Name};terms={parameter.TermCount};selected={selected};preprocess={preprocessing.DerivativeOrder},{preprocessing.DifferenceGap},{preprocessing.SmoothWindow1},{preprocessing.SmoothWindow2};bias={parameter.Bias:G9};mean={parameter.EquationMean:G9};sec={parameter.EquationSec:G9};rsq={parameter.EquationRsq:G9};firstCoeff={parameter.Coefficients[0]:G9}");
    }
    return 0;
}

if (args.Length == 3 && args[0].Equals("--predict-legacy-eqa", StringComparison.OrdinalIgnoreCase))
{
    var model = LegacyEqaModel.ReadNir5000(args[1]);
    var range = new ScanRange(model.Container.StartWavelengthNm, model.Container.StopWavelengthNm, model.Container.IncrementWavelengthNm);
    var nir = LegacyNirFile.Read(args[2], range);
    Console.WriteLine($"Legacy EQA prediction; instrument={nir.Metadata.InstrumentName}; points={nir.Metadata.PointCount}; provenance=replay");
    foreach (var parameter in model.Parameters)
    {
        var value = parameter.PredictSpectrum(nir.Spectrum.Values.ToArray());
        Console.WriteLine($"{parameter.Name};prediction={value:G9};mean={parameter.EquationMean:G9};sec={parameter.EquationSec:G9};rsq={parameter.EquationRsq:G9}");
    }
    return 0;
}

if (args.Length == 3 && args[0].Equals("--export-nova-sample", StringComparison.OrdinalIgnoreCase))
{
    var novaSpectrumInput = Path.GetExtension(args[1]).Equals(".nir", StringComparison.OrdinalIgnoreCase)
        ? LegacyNirFile.Read(args[1], new ScanRange(1100, 2498, 2)).Spectrum
        : LoadSpectrumCsv(args[1]);
    NovaOpenInstrumentExport.WriteJson(args[2], NovaOpenInstrumentExport.Create(novaSpectrumInput));
    Console.WriteLine($"Nova OpenInstrument sample JSON written to {Path.GetFullPath(args[2])}; provenance=replay/simulator");
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

if (args.Length == 6 && args[0].Equals("--export-foss-nir-csv2", StringComparison.OrdinalIgnoreCase))
{
    var model = LegacyEqaModel.ReadNir5000(args[1]);
    var limsSpectrum = LegacyNirFile.Read(args[2], new ScanRange(1100, 2498, 2)).Spectrum;
    if (limsSpectrum.Values.Count != model.Container.SourcePointCount)
        throw new InvalidDataException($"Spectrum has {limsSpectrum.Values.Count} points; model requires {model.Container.SourcePointCount}.");
    var calibrationParts = args[4].Split('-', 2, StringSplitOptions.TrimEntries);
    var document = new FossNirCommaSep2Document(
        args[3],
        DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        "NIRSystem 5000",
        calibrationParts[0],
        calibrationParts.Length == 2 ? calibrationParts[1] : string.Empty,
        model.Parameters.Select(parameter => new FossNirResultRow(parameter.Name, parameter.PredictSpectrum(limsSpectrum.Values.ToArray()))).ToArray());
    document.Write(args[5]);
    Console.WriteLine($"FossNirCommaSep2 result file written to {Path.GetFullPath(args[5])}; parameters={model.Parameters.Count}; provenance=replay");
    return 0;
}

if (args.Length == 4 && args[0].Equals("--inspect-capture-pair", StringComparison.OrdinalIgnoreCase))
{
    var transmit = File.ReadAllBytes(args[1]);
    var receive = File.ReadAllBytes(args[2]);
    var payloadLength = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
    var txFrames = Nir5000Capture.FindFrames(transmit, payloadLength);
    var rxFrames = Nir5000Capture.FindFrames(receive, payloadLength);
    Console.WriteLine($"TX capture bytes: {transmit.Length}; valid frames: {txFrames.Count}");
    Console.WriteLine($"RX capture bytes: {receive.Length}; valid frames: {rxFrames.Count}");
    foreach (var frame in txFrames)
        Console.WriteLine($"TX {frame.Frame.Header[0]:X2}{frame.Frame.Header[1]:X2}{frame.Frame.Header[2]:X2}{frame.Frame.Header[3]:X2}; offset={frame.Offset}; checksum={frame.Frame.Checksum:X2}");
    foreach (var frame in rxFrames)
        Console.WriteLine($"RX {frame.Frame.Header[0]:X2}{frame.Frame.Header[1]:X2}{frame.Frame.Header[2]:X2}{frame.Frame.Header[3]:X2}; offset={frame.Offset}; checksum={frame.Frame.Checksum:X2}");
    return 0;
}

if (args.Length == 2 && args[0].Equals("--inspect-diagnostics", StringComparison.OrdinalIgnoreCase))
{
    var diagnostic = LegacyDiagnosticSettings.Load(args[1]);
    var baud = Nir5000BaudRates.TryResolve(diagnostic.BaudCode, out var resolvedBaud) ? resolvedBaud.ToString() : "unknown";
    Console.WriteLine($"ModelType={diagnostic.ModelType}; serial={diagnostic.SerialNumber}; port={diagnostic.Port}; baudCode={diagnostic.BaudCode} ({baud})");
    Console.WriteLine($"Visible={diagnostic.VisibleRange.StartNm:G}-{diagnostic.VisibleRange.EndNm:G} nm step={diagnostic.VisibleRange.IncrementNm:G}");
    Console.WriteLine($"NIR={diagnostic.NirRange.StartNm:G}-{diagnostic.NirRange.EndNm:G} nm step={diagnostic.NirRange.IncrementNm:G}");
    Console.WriteLine($"ReferenceBefore={diagnostic.ReferenceBefore}; average={diagnostic.ScanAverage}; referenceAfter={diagnostic.ReferenceAfter}");
    Console.WriteLine($"Reference wavelengths={string.Join(", ", diagnostic.ReferenceWavelengths.Select(value => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)))}");
    var invariant = System.Globalization.CultureInfo.InvariantCulture;
    Console.WriteLine($"NIR K/Phi={diagnostic.NirK.ToString("G", invariant)}/{diagnostic.NirPhi.ToString("G", invariant)}; VIS K/Phi={diagnostic.VisibleK.ToString("G", invariant)}/{diagnostic.VisiblePhi.ToString("G", invariant)}");
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
Console.Error.WriteLine("Usage: Nir5000.Cli <Prod5000.ini> [output.csv] | --inspect-project <Prod5000.ini> | --inspect-diagnostics <Diagnstc.ini> | --inspect-nir ... | --inspect-eqa <model.json> <spectrum.csv> | --inspect-legacy-eqa <model.eqa> | --inspect-legacy-eqa-model <model.eqa> | --predict-legacy-eqa <model.eqa> <spectrum.nir> | --export-foss-nir-csv2 <model.eqa> <spectrum.nir> <ordno> <calibration[-version]> <output.txt> | --upload-starlims <file.txt> <endpoint> <accessKey> <secretKey> [apiMethod] | --export-nova-sample <spectrum.csv|spectrum.nir> <output.json> | --serial-tcp-bridge <COM> <baud> <port> [listenAddress] [txCapture.bin rxCapture.bin] | --inspect-capture <capture.bin> <payloadLength> | --inspect-capture-pair <tx.bin> <rx.bin> <payloadLength> | --inspect-calibration <buffer.bin>");
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

static Spectrum LoadSpectrumCsv(string path)
{
    var rows = File.ReadLines(path).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Split(',', StringSplitOptions.TrimEntries))
        .Where(parts => parts.Length >= 2)
        .Select(parts => (Wavelength: double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), Value: double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
        .ToArray();
    if (rows.Length == 0) throw new InvalidDataException("Spectrum CSV contains no data rows.");
    return new Spectrum(DateTimeOffset.UtcNow, rows.Select(row => row.Wavelength).ToArray(), rows.Select(row => row.Value).ToArray());
}

sealed class CapturingNovaHandler : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("accepted")
        };
    }
}

sealed class CapturingStarLimsHandler : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }
    public string? Body { get; private set; }
    public string? ContentType { get; private set; }
    public string? FileName { get; private set; }
    public string? Signature { get; private set; }
    public string? Timestamp { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        ContentType = request.Content?.Headers.ContentType?.MediaType;
        FileName = request.Headers.TryGetValues("SL-API-Filename", out var fileNames) ? fileNames.Single() : null;
        Signature = request.Headers.TryGetValues("SL-API-Signature", out var signatures) ? signatures.Single() : null;
        Timestamp = request.Headers.TryGetValues("SL-API-Timestamp", out var timestamps) ? timestamps.Single() : null;
        return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("accepted") };
    }
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

sealed class RecordingWire : INir5000WireRecorder
{
    public List<byte> Transmit { get; } = new();
    public List<byte> Receive { get; } = new();
    public void RecordTransmit(ReadOnlySpan<byte> data) => Transmit.AddRange(data.ToArray());
    public void RecordReceive(byte value) => Receive.Add(value);
}
