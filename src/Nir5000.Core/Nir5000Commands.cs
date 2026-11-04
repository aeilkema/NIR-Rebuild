namespace Nir5000.Core;

public static class Nir5000Commands
{
    /// <summary>Command observed in the legacy 6500.dll GET_SERNO path.</summary>
    public const byte GetSerialNumber = 0x5B;

    /// <summary>Command observed in the legacy 6500.dll GET_NIR/GET_RESPONSE paths.</summary>
    public const byte GetSpectrum = 0x5D;

    /// <summary>Maximum transfer segment used by the legacy segmented transfer path.</summary>
    public const int MaximumSegmentLength = 0xF0;
}

public sealed record Nir5000Command(byte Code, ReadOnlyMemory<byte> Payload);
