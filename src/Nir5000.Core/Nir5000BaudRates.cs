namespace Nir5000.Core;

public static class Nir5000BaudRates
{
    private static readonly IReadOnlyDictionary<int, int> known = new Dictionary<int, int>
    {
        [0] = 1200,
        [1] = 2400,
        [2] = 4800,
        [3] = 9600,
        [4] = 19200,
        [5] = 38400
    };

    public static bool TryResolve(int legacyCode, out int baudRate) => known.TryGetValue(legacyCode, out baudRate);
}
