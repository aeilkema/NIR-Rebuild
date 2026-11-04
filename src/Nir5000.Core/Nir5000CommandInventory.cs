namespace Nir5000.Core;

public sealed record Nir5000CommandEvidence(
    string Name,
    byte Code,
    string PayloadOrExchangeLength,
    string Status,
    string Evidence);

public static class Nir5000CommandInventory
{
    public static IReadOnlyList<Nir5000CommandEvidence> All { get; } =
    [
        new("Typed command 0x5A", Nir5000Commands.Command5A, "3 bytes", "ID confirmed; meaning unresolved", "Nirsinst.dll typed wrapper"),
        new("Serial number", Nir5000Commands.GetSerialNumber, "3-byte typed exchange; 8-byte transfer path", "ID and transfer path confirmed", "Nirsinst.dll and 6500.dll GET_SERNO"),
        new("Typed command 0x5C", Nir5000Commands.Command5C, "2 bytes", "ID confirmed; meaning unresolved", "Nirsinst.dll typed wrapper"),
        new("Spectrum/response", Nir5000Commands.GetSpectrum, "2-byte typed exchange; segmented response path", "ID confirmed; payload conversion still model-dependent", "Nirsinst.dll and 6500.dll GET_NIR/GET_RESPONSE"),
        new("Typed command 0x5E", Nir5000Commands.Command5E, "8 bytes", "ID confirmed; meaning unresolved", "Nirsinst.dll typed wrapper"),
        new("Lamp on", Nir5000LampCommands.SetLampRequest, "02 FF", "Payload confirmed; no device acknowledgement in legacy path", "Nirsinst.dll InstrSetLamp -> SicWrite"),
        new("Lamp off", Nir5000LampCommands.SetLampRequest, "02 00", "Payload confirmed; no device acknowledgement in legacy path", "Nirsinst.dll InstrSetLamp -> SicWrite"),
        new("LED pass/fail internal", 0x09, "02 bytes", "Internal write observed; user operation unresolved", "Nirsinst.dll InstrSetLEDPassFail disassembly")
    ];
}
