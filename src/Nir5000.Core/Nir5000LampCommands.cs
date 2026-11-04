namespace Nir5000.Core;

/// <summary>
/// The legacy Nirsinst.dll InstrSetLamp implementation constructs this
/// two-byte device request before handing it to its private transport layer.
/// It is not, by itself, a complete ENQ/frame payload.
/// </summary>
public static class Nir5000LampCommands
{
    public const byte SetLampRequest = 0x02;
    public const byte LampOffValue = 0x00;
    public const byte LampOnValue = 0xFF;

    public static byte[] BuildObservedRequest(bool enabled) =>
        [SetLampRequest, enabled ? LampOnValue : LampOffValue];
}
