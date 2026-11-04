namespace Nir5000.Core;

public sealed record LegacyDiagnosticOperationEvidence(string Name, string Category, string Status, string Evidence);

public static class LegacyDiagnosticOperationInventory
{
    public static IReadOnlyList<LegacyDiagnosticOperationEvidence> All { get; } =
    [
        new("GET_INSTHDR", "instrument header", "named operation; wire payload unresolved", "DIAGNSTC.EXE static string"),
        new("GET_INSTMODEL", "instrument metadata", "named operation; wire payload unresolved", "DIAGNSTC.EXE static string"),
        new("GET_INSTPARM", "instrument parameters", "named operation; wire payload unresolved", "DIAGNSTC.EXE static string"),
        new("GET_NIR", "spectrum", "named operation; command path linked to 0x5D", "DIAGNSTC.EXE/6500.dll static evidence"),
        new("GET_RESPONSE", "instrument response", "named operation; wire payload unresolved", "DIAGNSTC.EXE static string"),
        new("GET_NIRGAIN", "NIR gain", "named operation; write sequence unresolved", "DIAGNSTC.EXE static string"),
        new("GET_VISGAIN", "visible gain", "named operation; write sequence unresolved", "DIAGNSTC.EXE static string"),
        new("GET_K / GET_PHI", "statistics", "named operation; payload unresolved", "DIAGNSTC.EXE static strings"),
        new("PUT_K / PUT_PHI", "statistics upload", "named operation; payload unresolved", "DIAGNSTC.EXE static strings"),
        new("DOWNLOAD_K_AND_PHI", "statistics download", "named operation; payload unresolved", "DIAGNSTC.EXE static string"),
        new("SET_DIAGNOSTICS_FLAG", "diagnostic state", "named operation; payload unresolved", "DIAGNSTC.EXE static string"),
        new("REGISTER_NIRQA_INI", "diagnostic configuration", "named operation; payload unresolved", "DIAGNSTC.EXE static string"),
        new("WAVEACC", "wavelength accuracy", "test boundary identified; wire sequence unresolved", "DIAGNSTC.EXE resources/strings"),
        new("NIRPEAT", "NIR repeatability", "test boundary identified; wire sequence unresolved", "DIAGNSTC.EXE resources/strings"),
        new("RESPONSE", "instrument response test", "test boundary identified; wire sequence unresolved", "DIAGNSTC.EXE resources/strings")
    ];
}
