using System.Globalization;
using System.Text;

namespace Nir5000.Core;

public sealed record FossNirResultRow(
    string Analyte,
    double Result,
    double GlobalH = 0,
    double NeighborhoodH = 0,
    double TStatistic = 0,
    double StandardDeviation = 0,
    double Minimum = 0,
    double Maximum = 0,
    double ActualMinimum = 0,
    double WarningMinimum = 0,
    double ActualMaximum = 0,
    double WarningMaximum = 0,
    int OverallSeverity = 0,
    int GlobalHSeverity = 0,
    int NeighborhoodHSeverity = 0,
    int TStatisticSeverity = 0,
    int StandardDeviationSeverity = 0,
    int MinimumSeverity = 0,
    int MaximumSeverity = 0);

/// <summary>Writes the semicolon-delimited file consumed by FossNirCommaSep2.</summary>
public sealed record FossNirCommaSep2Document(
    string OrderNumber,
    string DateText,
    string Model,
    string CalibrationCode,
    string CalibrationVersion,
    IReadOnlyList<FossNirResultRow> Results)
{
    public string ToText()
    {
        var builder = new StringBuilder();
        builder.Append("HDR;").Append(OrderNumber).Append(';').Append(DateText).Append(';')
            .Append(Model).Append(';').Append(CalibrationCode);
        if (!string.IsNullOrWhiteSpace(CalibrationVersion)) builder.Append('-').Append(CalibrationVersion);
        builder.AppendLine();
        foreach (var row in Results)
        {
            builder.Append("PS;").Append(row.Analyte).Append(';').Append(Number(row.Result)).Append(';')
                .Append(Number(row.GlobalH)).Append(';').Append(Number(row.NeighborhoodH)).Append(';')
                .Append(Number(row.TStatistic)).Append(';').Append(Number(row.StandardDeviation)).Append(';')
                .Append(Number(row.Minimum)).Append(';').Append(Number(row.Maximum)).Append(';')
                .Append(Number(row.ActualMinimum)).Append(';').Append(Number(row.WarningMinimum)).Append(';')
                .Append(Number(row.ActualMaximum)).Append(';').Append(Number(row.WarningMaximum)).AppendLine();
            builder.Append("SE;").Append(row.Analyte).Append(';').Append(row.OverallSeverity).Append(';')
                .Append(row.GlobalHSeverity).Append(';').Append(row.NeighborhoodHSeverity).Append(';')
                .Append(row.TStatisticSeverity).Append(';').Append(row.StandardDeviationSeverity).Append(';')
                .Append(row.MinimumSeverity).Append(';').Append(row.MaximumSeverity).AppendLine();
        }
        return builder.ToString();
    }

    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, ToText(), new UTF8Encoding(false));
    }

    private static string Number(double value) => value.ToString("G", CultureInfo.InvariantCulture);
}
