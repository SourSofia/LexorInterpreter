namespace LexorInterpreter;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

    // Token helpers
    private Token Current       => _tokens[_pos];
    private bool  Check(TokenType t) => Current.Type == t;
    private bool  Match(TokenType t) { if (Check(t)) { Consume(); return true; } return false; }

    private Token Consume()
    {
        var t = _tokens[_pos]; _pos++; return t;
    }

    private Token Expect(TokenType type)
    {
        if (!Check(type))
            throw new LexorException(
                $"Expected {type} but got {Current.Type} (\"{Current.Value}\") at line {Current.Line}");
        return Consume();
    }

    private void SkipNewlines()   { while (Check(TokenType.NEWLINE)) Consume(); }
    private void SkipToNewline()
    {
        while (!Check(TokenType.NEWLINE) && !Check(TokenType.EOF)) Consume();
        if (Check(TokenType.NEWLINE)) Consume();
    }

    // Entry point
    public ProgramNode Parse()
    {
        var program = new ProgramNode();
        SkipNewlines();
        Expect(TokenType.SCRIPT_AREA);
        SkipNewlines();
        Expect(TokenType.START_SCRIPT);
        SkipNewlines();

        // DECLARE statements come first
        while (Check(TokenType.DECLARE))
        {
            program.Statements.Add(ParseDeclare());
            SkipNewlines();
        }

        // Executable statements until END SCRIPT
        while (!Check(TokenType.END_SCRIPT) && !Check(TokenType.EOF))
        {
            SkipNewlines();
            if (Check(TokenType.END_SCRIPT) || Check(TokenType.EOF)) break;
            program.Statements.Add(ParseStatement());
            SkipNewlines();
        }

        Expect(TokenType.END_SCRIPT);
        SkipNewlines();
        if (!Check(TokenType.EOF))
            throw new LexorException(
                $"Unexpected content after END SCRIPT at line {Current.Line}");
        return program;
    }

    // Statement dispatcher
    private StatementNode ParseStatement() => Current.Type switch
    {
        TokenType.PRINT        => ParsePrint(),
        TokenType.SCAN         => ParseScan(),
        TokenType.IF           => ParseIf(),
        TokenType.FOR          => ParseFor(),
        TokenType.REPEAT_WHEN  => ParseRepeatWhen(),    // spec while loop
        TokenType.DO_REPEAT    => ParseDoRepeatUntil(), // custom do-while
        TokenType.IDENTIFIER   => ParseAssign(),
        _ => throw new LexorException(
                $"Unexpected token {Current.Type} (\"{Current.Value}\") at line {Current.Line}")
    };

    private DeclareNode ParseDeclare()
    {
        Expect(TokenType.DECLARE);
        var node = new DeclareNode();
        node.DataType = Current.Type switch
        {
            TokenType.INT   => "INT",
            TokenType.FLOAT => "FLOAT",
            TokenType.CHAR  => "CHAR",
            TokenType.BOOL  => "BOOL",
            _ => throw new LexorException($"Expected data type at line {Current.Line}")
        };
        Consume();
        do
        {
            string name = Expect(TokenType.IDENTIFIER).Value;
            ExpressionNode? init = null;
            if (Match(TokenType.ASSIGN)) init = ParseExpression();
            node.Variables.Add((name, init));
        } while (Match(TokenType.COMMA));
        SkipToNewline();
        return node;
    }

    //newly added parseassign()
    private StatementNode ParseAssign()
    {
        var names = new List<string>();
        names.Add(Expect(TokenType.IDENTIFIER).Value);

        while (Check(TokenType.ASSIGN))
        {
            Consume(); // consume =
            if (Check(TokenType.IDENTIFIER) && _pos + 1 < _tokens.Count
                && _tokens[_pos + 1].Type == TokenType.ASSIGN)
            {
                names.Add(Consume().Value); // still chaining
            }
            else
            {
                // reached the final value expression
                var value = ParseExpression();
                SkipToNewline();
                return new MultiAssignNode { VariableNames = names, Value = value };
            }
        }

        SkipToNewline();
        return new MultiAssignNode { VariableNames = names, Value = new IntLiteralNode(0) };
    }
    private PrintNode ParsePrint()
    {
        Expect(TokenType.PRINT);
        Expect(TokenType.COLON);
        var node = new PrintNode();
        node.Parts.Add(ParsePrintPart());
        while (Check(TokenType.AMPERSAND)) { Consume(); node.Parts.Add(ParsePrintPart()); }
        SkipToNewline();
        return node;
    }

    private ExpressionNode ParsePrintPart()
    {
        if (Check(TokenType.DOLLAR))   { Consume(); return new NewlineNode(); }
        if (Check(TokenType.LBRACKET))
        {
            Consume();
            string content = Expect(TokenType.STRING_LITERAL).Value;
            Expect(TokenType.RBRACKET);
            return new EscapeNode(content);
        }
        return ParseExpression();
    }

    private ScanNode ParseScan()
    {
        Expect(TokenType.SCAN);
        Expect(TokenType.COLON);
        var node = new ScanNode();
        node.Variables.Add(Expect(TokenType.IDENTIFIER).Value);
        while (Match(TokenType.COMMA)) node.Variables.Add(Expect(TokenType.IDENTIFIER).Value);
        SkipToNewline();
        return node;
    }

    private IfNode ParseIf()
    {
        var node = new IfNode();
        Expect(TokenType.IF);
        Expect(TokenType.LPAREN);
        var cond = ParseExpression();
        Expect(TokenType.RPAREN);
        SkipNewlines();
        Expect(TokenType.START_IF);
        SkipNewlines();
        var body = ParseBlock(TokenType.END_IF, TokenType.ELSE, TokenType.ELSE_IF);
        node.Branches.Add((cond, body));
        if (Match(TokenType.END_IF)) { SkipToNewline(); return node; }

        while (Check(TokenType.ELSE_IF))
        {
            Consume();
            Expect(TokenType.LPAREN); var eic = ParseExpression(); Expect(TokenType.RPAREN);
            SkipNewlines(); Expect(TokenType.START_IF); SkipNewlines();
            var eib = ParseBlock(TokenType.END_IF, TokenType.ELSE, TokenType.ELSE_IF);
            node.Branches.Add((eic, eib));
            if (Match(TokenType.END_IF)) { SkipToNewline(); return node; }
        }

        if (Check(TokenType.ELSE))
        {
            Consume(); SkipNewlines(); Expect(TokenType.START_IF); SkipNewlines();
            node.ElseBranch = ParseBlock(TokenType.END_IF);
            Expect(TokenType.END_IF); SkipToNewline();
        }
        return node;
    }

    private ForNode ParseFor()
    {
        Expect(TokenType.FOR); Expect(TokenType.LPAREN);
        string iv = Expect(TokenType.IDENTIFIER).Value; Expect(TokenType.ASSIGN);
        var initVal = ParseExpression(); Expect(TokenType.COMMA);
        var cond = ParseExpression(); Expect(TokenType.COMMA);
        string uv = Expect(TokenType.IDENTIFIER).Value; Expect(TokenType.ASSIGN);
        var updateVal = ParseExpression();
        Expect(TokenType.RPAREN); SkipNewlines(); Expect(TokenType.START_FOR); SkipNewlines();
        var body = ParseBlock(TokenType.END_FOR);
        Expect(TokenType.END_FOR); SkipToNewline();
        var node = new ForNode
        {
            Initialization = new AssignNode { VariableName = iv, Value = initVal },
            Condition      = cond,
            Update         = new AssignNode { VariableName = uv, Value = updateVal },
        };
        node.Body.AddRange(body);
        return node;
    }

    // REPEAT WHEN — spec built-in while loop
    // Checks condition BEFORE running the body.
    // Syntax:
    //   REPEAT WHEN (<condition>)
    //   START REPEAT
    //     <statements>
    //   END REPEAT
    private RepeatWhenNode ParseRepeatWhen()
    {
        Expect(TokenType.REPEAT_WHEN);
        Expect(TokenType.LPAREN);
        var cond = ParseExpression();
        Expect(TokenType.RPAREN);
        SkipNewlines();
        Expect(TokenType.START_REPEAT);
        SkipNewlines();
        var body = ParseBlock(TokenType.END_REPEAT);
        Expect(TokenType.END_REPEAT);
        SkipToNewline();
        var node = new RepeatWhenNode { Condition = cond };
        node.Body.AddRange(body);
        return node;
    }

    // DO REPEAT UNTIL — our custom do-while loop
    // Runs body FIRST, then checks condition at the end.
    // Syntax:
    //   DO REPEAT
    //   START REPEAT
    //     <statements>
    //   END REPEAT
    //   UNTIL (<condition>)
    private DoRepeatUntilNode ParseDoRepeatUntil()
    {
        Expect(TokenType.DO_REPEAT);
        SkipNewlines();
        Expect(TokenType.START_REPEAT);
        SkipNewlines();
        var body = ParseBlock(TokenType.END_REPEAT);
        Expect(TokenType.END_REPEAT);
        SkipNewlines();
        // UNTIL (condition) comes AFTER the body
        Expect(TokenType.UNTIL);
        Expect(TokenType.LPAREN);
        var cond = ParseExpression();
        Expect(TokenType.RPAREN);
        SkipToNewline();
        var node = new DoRepeatUntilNode { Condition = cond };
        node.Body.AddRange(body);
        return node;
    }

    // Reads statements until a stopper token is reached (does not consume stopper)
    private List<StatementNode> ParseBlock(params TokenType[] stoppers)
    {
        var stmts = new List<StatementNode>();
        while (!Check(TokenType.EOF))
        {
            SkipNewlines();
            if (stoppers.Contains(Current.Type)) break;
            stmts.Add(ParseStatement());
        }
        return stmts;
    }

    // Expression parsing (recursive descent)
    // Precedence low to high:
    //   OR -> AND -> NOT -> Comparison -> Add/Sub -> Mul/Div/Mod -> Unary -> Primary
    private ExpressionNode ParseExpression() => ParseOr();

    private ExpressionNode ParseOr()
    {
        var left = ParseAnd();
        while (Check(TokenType.OR)) { Consume(); left = new BinaryOpNode(left, "OR", ParseAnd()); }
        return left;
    }

    private ExpressionNode ParseAnd()
    {
        var left = ParseNot();
        while (Check(TokenType.AND)) { Consume(); left = new BinaryOpNode(left, "AND", ParseNot()); }
        return left;
    }

    private ExpressionNode ParseNot()
    {
        if (Check(TokenType.NOT)) { Consume(); return new UnaryOpNode("NOT", ParseNot()); }
        return ParseComparison();
    }

    private ExpressionNode ParseComparison()
    {
        var left = ParseAddSub();
        while (Current.Type is TokenType.EQUAL or TokenType.NOT_EQUAL
                             or TokenType.LESS  or TokenType.GREATER
                             or TokenType.LESS_EQUAL or TokenType.GREATER_EQUAL)
        {
            string op = Consume().Value;
            left = new BinaryOpNode(left, op, ParseAddSub());
        }
        return left;
    }

    private ExpressionNode ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Current.Type is TokenType.PLUS or TokenType.MINUS)
        {
            string op = Consume().Value;
            left = new BinaryOpNode(left, op, ParseMulDiv());
        }
        return left;
    }

    private ExpressionNode ParseMulDiv()
    {
        var left = ParseUnary();
        while (Current.Type is TokenType.MULTIPLY or TokenType.DIVIDE or TokenType.MODULO)
        {
            string op = Consume().Value;
            left = new BinaryOpNode(left, op, ParseUnary());
        }
        return left;
    }

    private ExpressionNode ParseUnary()
    {
        if (Check(TokenType.MINUS)) { Consume(); return new UnaryOpNode("-", ParseUnary()); }
        if (Check(TokenType.PLUS))  { Consume(); return ParseUnary(); }
        return ParsePrimary();
    }

    private ExpressionNode ParsePrimary() => Current.Type switch
    {
        TokenType.INTEGER_LITERAL => new IntLiteralNode   (int.Parse(Consume().Value)),
        TokenType.FLOAT_LITERAL   => new FloatLiteralNode (double.Parse(Consume().Value,
                                         System.Globalization.CultureInfo.InvariantCulture)),
        TokenType.BOOL_LITERAL    => new BoolLiteralNode  (Consume().Value == "TRUE"),
        TokenType.CHAR_LITERAL    => new CharLiteralNode  (Consume().Value[0]),
        TokenType.STRING_LITERAL  => new StringLiteralNode(Consume().Value),
        TokenType.IDENTIFIER      => new VariableNode     (Consume().Value),
        TokenType.LPAREN          => ParseGrouped(),
        _ => throw new LexorException(
                $"Unexpected token in expression: {Current.Type} (\"{Current.Value}\") at line {Current.Line}")
    };

    private ExpressionNode ParseGrouped()
    {
        Expect(TokenType.LPAREN);
        var expr = ParseExpression();
        Expect(TokenType.RPAREN);
        return expr;
    }
}