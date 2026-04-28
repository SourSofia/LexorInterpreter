// Evaluator.cs
namespace LexorInterpreter;

public class Evaluator
{
    // Runtime variable values: name -> value (int / double / char / bool)
    private readonly Dictionary<string, object> _vars  = new();
    // Declared type for each variable: name -> "INT" / "FLOAT" / "CHAR" / "BOOL"
    private readonly Dictionary<string, string> _types = new();

    // Entry point
    public void Execute(ProgramNode program)
    {
        foreach (var stmt in program.Statements)
            ExecuteStatement(stmt);
    }

    // Statement dispatcher
    private void ExecuteStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case DeclareNode       d: ExecuteDeclare(d);       break;
            case AssignNode        a: ExecuteAssign(a);        break;
            case MultiAssignNode   m: ExecuteMultiAssign(m);   break;  // newly added
            case PrintNode         p: ExecutePrint(p);         break;
            case ScanNode          s: ExecuteScan(s);          break;
            case IfNode            i: ExecuteIf(i);            break;
            case ForNode           f: ExecuteFor(f);           break;
            case RepeatWhenNode    r: ExecuteRepeatWhen(r);    break;
            case DoRepeatUntilNode d: ExecuteDoRepeatUntil(d); break;
            default: throw new LexorException($"Unknown statement: {stmt.GetType().Name}");
        }
    }

    private void ExecuteDeclare(DeclareNode node)
    {
        foreach (var (name, initExpr) in node.Variables)
        {
            if (_types.ContainsKey(name))
                throw new LexorException($"Variable '{name}' is already declared.");
            _types[name] = node.DataType;
            object defaultVal = node.DataType switch
            {
                "INT"   => 0,
                "FLOAT" => 0.0,
                "CHAR"  => '\0',
                "BOOL"  => false,
                _ => throw new LexorException($"Unknown type '{node.DataType}'")
            };
            _vars[name] = initExpr != null
                ? CoerceToType(EvalExpr(initExpr), node.DataType, name)
                : defaultVal;
        }
    }

    private void ExecuteAssign(AssignNode node)
    {
        if (!_types.TryGetValue(node.VariableName, out var typeName))
            throw new LexorException($"Undeclared variable '{node.VariableName}'.");
        _vars[node.VariableName] =
            CoerceToType(EvalExpr(node.Value), typeName, node.VariableName);
    }

    private void ExecuteMultiAssign(MultiAssignNode node)
    {
        var evaluated = EvalExpr(node.Value);
        foreach (var name in node.VariableNames)
        {
            if (!_types.TryGetValue(name, out var typeName))
                throw new LexorException($"Undeclared variable '{name}'.");
            _vars[name] = CoerceToType(evaluated, typeName, name);
        }
    }



    private void ExecutePrint(PrintNode node)
    {
        foreach (var part in node.Parts)
        {
            switch (part)
            {
                case NewlineNode:  Console.WriteLine();                    break;
                case EscapeNode e: Console.Write(e.Content);              break;
                default:           Console.Write(FormatValue(EvalExpr(part))); break;
            }
        }
    }

    // BOOL must print as TRUE/FALSE, not C#'s True/False
    private static string FormatValue(object val) => val switch
    {
        bool b => b ? "TRUE" : "FALSE",
        char c => c.ToString(),
        _      => val.ToString() ?? ""
    };

    private void ExecuteScan(ScanNode node)
    {
        Console.Write("Input: ");
        string raw   = Console.ReadLine() ?? "";
        var    parts = raw.Split(',').Select(p => p.Trim()).ToArray();
        for (int i = 0; i < node.Variables.Count; i++)
        {
            string varName = node.Variables[i];
            if (!_types.TryGetValue(varName, out var typeName))
                throw new LexorException($"Undeclared variable '{varName}'.");
            string input = i < parts.Length ? parts[i] : "";
            _vars[varName] = ParseInput(input, typeName, varName);
        }
    }

    private static object ParseInput(string raw, string typeName, string varName) =>
        typeName switch
        {
            "INT"   => int.TryParse(raw, out var i) ? i
                        : throw new LexorException($"Cannot read '{raw}' as INT for '{varName}'"),
            "FLOAT" => double.TryParse(raw,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d
                        : throw new LexorException($"Cannot read '{raw}' as FLOAT for '{varName}'"),
            "CHAR"  => raw.Length == 1 ? (object)raw[0]
                        : throw new LexorException($"Cannot read '{raw}' as CHAR for '{varName}'"),
            "BOOL"  => raw.ToUpper() switch
                        {
                            "TRUE"  => (object)true,
                            "FALSE" => false,
                            _ => throw new LexorException($"Cannot read '{raw}' as BOOL for '{varName}'")
                        },
            _ => throw new LexorException($"Unknown type '{typeName}'")
        };

    private void ExecuteIf(IfNode node)
    {
        foreach (var (cond, body) in node.Branches)
        {
            if (IsTruthy(EvalExpr(cond)))
            {
                foreach (var s in body) ExecuteStatement(s);
                return;
            }
        }
        if (node.ElseBranch != null)
            foreach (var s in node.ElseBranch) ExecuteStatement(s);
    }

    private void ExecuteFor(ForNode node)
    {
        ExecuteAssign(node.Initialization);
        while (IsTruthy(EvalExpr(node.Condition)))
        {
            foreach (var s in node.Body) ExecuteStatement(s);
            ExecuteAssign(node.Update);
        }
    }

    // REPEAT WHEN — spec built-in while loop
    // Condition checked BEFORE body. Zero or more iterations.
    private void ExecuteRepeatWhen(RepeatWhenNode node)
    {
        while (IsTruthy(EvalExpr(node.Condition)))
            foreach (var s in node.Body) ExecuteStatement(s);
    }

    // DO REPEAT UNTIL — our custom do-while loop
    // Body runs FIRST, condition checked AFTER. One or more iterations.
    private void ExecuteDoRepeatUntil(DoRepeatUntilNode node)
    {
        do
        {
            foreach (var s in node.Body) ExecuteStatement(s);
        }
        while (IsTruthy(EvalExpr(node.Condition)));
    }

    // Expression evaluator
    private object EvalExpr(ExpressionNode expr) => expr switch
    {
        IntLiteralNode    n => n.Value,
        FloatLiteralNode  n => n.Value,
        BoolLiteralNode   n => n.Value,
        CharLiteralNode   n => n.Value,
        StringLiteralNode n => n.Value,
        VariableNode      v => GetVar(v.Name),
        UnaryOpNode       u => EvalUnary(u),
        BinaryOpNode      b => EvalBinary(b),
        NewlineNode         => "\n",
        EscapeNode        e => e.Content,
        _ => throw new LexorException($"Unknown expression: {expr.GetType().Name}")
    };

    private object GetVar(string name)
    {
        if (!_vars.TryGetValue(name, out var val))
            throw new LexorException($"Undefined variable '{name}'.");
        return val;
    }

    private object EvalUnary(UnaryOpNode node)
    {
        var val = EvalExpr(node.Operand);
        return node.Op switch
        {
            "-"   => val switch
                     {
                         int    i => -i,
                         double d => -d,
                         _ => throw new LexorException($"Cannot negate {val.GetType().Name}")
                     },
            "NOT" => val switch
                     {
                         bool b => !b,
                         _ => throw new LexorException("NOT requires a BOOL value")
                     },
            _ => val
        };
    }

    private object EvalBinary(BinaryOpNode node)
    {
        var left  = EvalExpr(node.Left);
        var right = EvalExpr(node.Right);

        if (node.Op == "AND") return AsBool(left) && AsBool(right);
        if (node.Op == "OR")  return AsBool(left) || AsBool(right);

        // Arithmetic: promote to double if either side is double
        if (node.Op is "+" or "-" or "*" or "/" or "%")
        {
            if (left is double || right is double)
            {
                double l = ToDouble(left), r = ToDouble(right);
                return node.Op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => r == 0 ? throw new LexorException("Division by zero") : l / r,
                    "%" => l % r,
                    _   => throw new LexorException($"Unknown op '{node.Op}'")
                };
            }
            else
            {
                int l = AsInt(left), r = AsInt(right);
                return node.Op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => r == 0 ? throw new LexorException("Division by zero") : l / r,
                    "%" => l % r,
                    _   => throw new LexorException($"Unknown op '{node.Op}'")
                };
            }
        }

        // Comparison
        return node.Op switch
        {
            "==" => EqualValues(left, right),
            "<>" => !((bool)EqualValues(left, right)),
            "<"  => NumericCompare(left, right) < 0,
            ">"  => NumericCompare(left, right) > 0,
            "<=" => NumericCompare(left, right) <= 0,
            ">=" => NumericCompare(left, right) >= 0,
            _    => throw new LexorException($"Unknown operator '{node.Op}'")
        };
    }

    private static object EqualValues(object l, object r)
    {
        if (l is int li && r is double rd) return (double)li == rd;
        if (l is double ld && r is int ri) return ld == (double)ri;
        return l.Equals(r);
    }

    private static double NumericCompare(object l, object r) => ToDouble(l) - ToDouble(r);

    private static object CoerceToType(object val, string typeName, string varName) =>
        typeName switch
        {
            "INT"   => val switch
                       {
                           int    i => i,
                           double d => (int)d,
                           _ => throw new LexorException(
                               $"Cannot assign {val.GetType().Name} to INT variable '{varName}'")
                       },
            "FLOAT" => val switch
                       {
                           double d => d,
                           int    i => (double)i,
                           _ => throw new LexorException(
                               $"Cannot assign {val.GetType().Name} to FLOAT variable '{varName}'")
                       },
            "CHAR"  => val switch
                       {
                           char c => c,
                           _ => throw new LexorException(
                               $"Cannot assign {val.GetType().Name} to CHAR variable '{varName}'")
                       },
            "BOOL"  => val switch
                       {
                           bool b => b,
                           _ => throw new LexorException(
                               $"Cannot assign {val.GetType().Name} to BOOL variable '{varName}'")
                       },
            _ => throw new LexorException($"Unknown type '{typeName}'")
        };

    private static bool   AsBool(object val)  => val is bool b   ? b : throw new LexorException($"Expected BOOL, got {val.GetType().Name}");
    private static int    AsInt(object val)    => val is int i    ? i : throw new LexorException($"Expected INT, got {val.GetType().Name}");
    private static double ToDouble(object val) => val switch
    {
        int    i => (double)i,
        double d => d,
        _ => throw new LexorException($"Cannot convert {val.GetType().Name} to number")
    };
    private static bool IsTruthy(object val) =>
        val is bool b ? b : throw new LexorException("Condition must evaluate to a BOOL expression");
}