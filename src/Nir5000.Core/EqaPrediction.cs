using System.Globalization;
using System.Text.Json;

namespace Nir5000.Core;

public sealed record EqaParameter(
    string Name,
    string Equation,
    double Slope = 1,
    double Bias = 0,
    double? EquationMean = null,
    double? EquationSec = null,
    double? EquationRsq = null);

public sealed record EqaModel(
    string Name,
    string InstrumentModel,
    int PointCount,
    double StartWavelengthNm,
    double IncrementNm,
    IReadOnlyList<EqaParameter> Parameters)
{
    public static EqaModel LoadJson(string path)
    {
        var model = JsonSerializer.Deserialize<EqaModel>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (model is null || string.IsNullOrWhiteSpace(model.Name) || model.Parameters.Count == 0)
            throw new InvalidDataException("EQA model JSON must contain a name and at least one parameter.");
        if (model.PointCount <= 0 || model.IncrementNm <= 0)
            throw new InvalidDataException("EQA model has invalid wavelength metadata.");
        return model;
    }
}

public sealed record EqaPrediction(
    string Parameter,
    double RawValue,
    double Value,
    double? EquationMean,
    double? EquationSec,
    double? EquationRsq);

public static class EqaPredictionEngine
{
    public static IReadOnlyList<EqaPrediction> Predict(EqaModel model, Spectrum spectrum)
    {
        if (spectrum.Values.Count != model.PointCount)
            throw new InvalidDataException($"Model expects {model.PointCount} points, spectrum contains {spectrum.Values.Count}.");
        if (spectrum.WavelengthsNm.Count != spectrum.Values.Count)
            throw new InvalidDataException("Spectrum wavelengths and values have different lengths.");
        if (spectrum.WavelengthsNm.Count > 0 && Math.Abs(spectrum.WavelengthsNm[0] - model.StartWavelengthNm) > 0.01)
            throw new InvalidDataException("Spectrum start wavelength does not match the EQA model.");

        var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < spectrum.Values.Count; i++)
        {
            var index = i + 1;
            variables[$"X{index}"] = spectrum.Values[i];
            variables[$"X_{index}"] = spectrum.Values[i];
            variables[$"W{Math.Round(spectrum.WavelengthsNm[i]):0}"] = spectrum.Values[i];
        }

        var predictions = new List<EqaPrediction>(model.Parameters.Count);
        foreach (var parameter in model.Parameters)
        {
            var raw = EqaExpression.Parse(parameter.Equation).Evaluate(variables);
            var value = raw * parameter.Slope + parameter.Bias;
            if (!double.IsFinite(raw) || !double.IsFinite(value))
                throw new InvalidDataException($"EQA prediction for '{parameter.Name}' is not finite.");
            predictions.Add(new EqaPrediction(parameter.Name, raw, value, parameter.EquationMean, parameter.EquationSec, parameter.EquationRsq));
        }
        return predictions;
    }
}

public sealed class EqaExpression
{
    private readonly Node root;
    private EqaExpression(Node root) => this.root = root;

    public static EqaExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new InvalidDataException("EQA equation is empty.");
        return new EqaExpression(new Parser(expression).Parse());
    }

    public double Evaluate(IReadOnlyDictionary<string, double> variables) => root.Evaluate(variables);

    private abstract class Node
    {
        public abstract double Evaluate(IReadOnlyDictionary<string, double> variables);
    }

    private sealed class NumberNode(double value) : Node
    {
        public override double Evaluate(IReadOnlyDictionary<string, double> variables) => value;
    }

    private sealed class VariableNode(string name) : Node
    {
        public override double Evaluate(IReadOnlyDictionary<string, double> variables) =>
            variables.TryGetValue(name, out var value)
                ? value
                : throw new InvalidDataException($"EQA equation references unknown variable '{name}'.");
    }

    private sealed class UnaryNode(char operation, Node operand) : Node
    {
        public override double Evaluate(IReadOnlyDictionary<string, double> variables)
        {
            var value = operand.Evaluate(variables);
            return operation == '-' ? -value : value;
        }
    }

    private sealed class BinaryNode(string operation, Node left, Node right) : Node
    {
        public override double Evaluate(IReadOnlyDictionary<string, double> variables)
        {
            var a = left.Evaluate(variables);
            var b = right.Evaluate(variables);
            return operation switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" when b != 0 => a / b,
                "/" => throw new InvalidDataException("EQA equation divides by zero."),
                "^" => Math.Pow(a, b),
                "=" => a == b ? 1 : 0,
                "<>" => a != b ? 1 : 0,
                "<" => a < b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                "<=" => a <= b ? 1 : 0,
                ">=" => a >= b ? 1 : 0,
                "&&" => a != 0 && b != 0 ? 1 : 0,
                "||" => a != 0 || b != 0 ? 1 : 0,
                _ => throw new InvalidDataException($"Unsupported EQA operator '{operation}'.")
            };
        }
    }

    private sealed class FunctionNode(string name, IReadOnlyList<Node> arguments) : Node
    {
        public override double Evaluate(IReadOnlyDictionary<string, double> variables)
        {
            var values = arguments.Select(argument => argument.Evaluate(variables)).ToArray();
            return name.ToLowerInvariant() switch
            {
                "abs" => One(values, Math.Abs),
                "sqrt" => One(values, Math.Sqrt),
                "exp" => One(values, Math.Exp),
                "log" => One(values, Math.Log),
                "log10" => One(values, Math.Log10),
                "sin" => One(values, Math.Sin),
                "cos" => One(values, Math.Cos),
                "tan" => One(values, Math.Tan),
                "min" => Many(values, values.Min),
                "max" => Many(values, values.Max),
                "if" when values.Length == 3 => values[0] != 0 ? values[1] : values[2],
                _ => throw new InvalidDataException($"Unsupported EQA function '{name}'.")
            };
        }

        private static double One(double[] values, Func<double, double> function) => values.Length == 1
            ? function(values[0])
            : throw new InvalidDataException("EQA unary function requires one argument.");

        private static double Many(double[] values, Func<double> function) => values.Length > 0
            ? function()
            : throw new InvalidDataException("EQA aggregate function requires an argument.");
    }

    private sealed class Parser(string text)
    {
        private int position;
        public Node Parse()
        {
            var result = ParseLogical();
            SkipWhitespace();
            if (position != text.Length) throw Error("Unexpected input.");
            return result;
        }

        private Node ParseLogical()
        {
            var node = ParseAnd();
            while (Try("||")) node = new BinaryNode("||", node, ParseAnd());
            return node;
        }

        private Node ParseAnd()
        {
            var node = ParseComparison();
            while (Try("&&")) node = new BinaryNode("&&", node, ParseComparison());
            return node;
        }

        private Node ParseComparison()
        {
            var node = ParseAdditive();
            while (true)
            {
                var operation = Try("<>") ? "<>" : Try("<=") ? "<=" : Try(">=") ? ">=" :
                    Try("=") ? "=" : Try("<") ? "<" : Try(">") ? ">" : null;
                if (operation is null) return node;
                node = new BinaryNode(operation, node, ParseAdditive());
            }
        }

        private Node ParseAdditive()
        {
            var node = ParseMultiplicative();
            while (true)
            {
                if (Try("+")) node = new BinaryNode("+", node, ParseMultiplicative());
                else if (Try("-")) node = new BinaryNode("-", node, ParseMultiplicative());
                else return node;
            }
        }

        private Node ParseMultiplicative()
        {
            var node = ParsePower();
            while (true)
            {
                if (Try("*")) node = new BinaryNode("*", node, ParsePower());
                else if (Try("/")) node = new BinaryNode("/", node, ParsePower());
                else return node;
            }
        }

        private Node ParsePower()
        {
            var node = ParseUnary();
            return Try("^") ? new BinaryNode("^", node, ParsePower()) : node;
        }

        private Node ParseUnary()
        {
            if (Try("+")) return new UnaryNode('+', ParseUnary());
            if (Try("-")) return new UnaryNode('-', ParseUnary());
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipWhitespace();
            if (Try("("))
            {
                var node = ParseAdditive();
                Expect(")");
                return node;
            }
            if (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.'))
                return new NumberNode(ParseNumber());
            var name = ParseName();
            if (Try("("))
            {
                var arguments = new List<Node>();
                if (!Try(")"))
                {
                    do arguments.Add(ParseLogical()); while (Try(","));
                    Expect(")");
                }
                return new FunctionNode(name, arguments);
            }
            return new VariableNode(name);
        }

        private double ParseNumber()
        {
            var start = position;
            while (position < text.Length && (char.IsDigit(text[position]) || ".eE+-".Contains(text[position])))
            {
                if ((text[position] == '+' || text[position] == '-') && position > start && text[position - 1] is not 'e' and not 'E') break;
                position++;
            }
            var token = text[start..position];
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw Error($"Invalid number '{token}'.");
        }

        private string ParseName()
        {
            SkipWhitespace();
            var start = position;
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_')) position++;
            if (start == position) throw Error("Expected number, variable or function.");
            return text[start..position];
        }

        private bool Try(string value)
        {
            SkipWhitespace();
            if (text.AsSpan(position).StartsWith(value, StringComparison.Ordinal)) { position += value.Length; return true; }
            return false;
        }

        private void Expect(string value)
        {
            if (!Try(value)) throw Error($"Expected '{value}'.");
        }

        private void SkipWhitespace() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private InvalidDataException Error(string message) => new($"Invalid EQA equation at position {position}: {message}");
    }
}
