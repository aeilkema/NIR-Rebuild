namespace Nir5000.Core;

public static class Nir5000Commands
{
    /// <summary>Typed command wrapper IDs statically observed in Nirsinst.dll.</summary>
    public const byte Command5A = 0x5A;
    /// <summary>Command observed in the legacy 6500.dll GET_SERNO path.</summary>
    public const byte GetSerialNumber = 0x5B;

    public const byte Command5C = 0x5C;

    /// <summary>Command observed in the legacy 6500.dll GET_NIR/GET_RESPONSE paths.</summary>
    public const byte GetSpectrum = 0x5D;

    public const byte Command5E = 0x5E;

    public const int ThreeByteExchangeLength = 3;
    public const int TwoByteExchangeLength = 2;
    public const int EightByteExchangeLength = 8;

    /// <summary>Maximum transfer segment used by the legacy segmented transfer path.</summary>
    public const int MaximumSegmentLength = 0xF0;
}

public sealed record Nir5000Command(byte Code, ReadOnlyMemory<byte> Payload);
