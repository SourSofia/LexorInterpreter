namespace LexorInterpreter;

// Base types
public abstract class AstNode        { }
public abstract class StatementNode  : AstNode { }
public abstract class ExpressionNode : AstNode { }

// Program root
public class ProgramNode : AstNode
{
    public List<StatementNode> Statements { get; } = new();
}

// DECLARE: DECLARE INT x, y=5
public class DeclareNode : StatementNode
{
    public string DataType { get; set; } = "";  // "INT", "FLOAT", "CHAR", "BOOL"
    public List<(string Name, ExpressionNode? InitialValue)> Variables { get; } = new();
}

// Assignment: x = expr
public class AssignNode : StatementNode
{
    public string         VariableName { get; set; } = "";
    public ExpressionNode Value        { get; set; } = null!;
}

//newly added
public class MultiAssignNode : StatementNode
{
    public List<string>   VariableNames { get; set; } = new();
    public ExpressionNode Value         { get; set; } = null!;
}




// PRINT: a & $ & "hi"
public class PrintNode : StatementNode
{
    public List<ExpressionNode> Parts { get; } = new();
}

// SCAN: x, y
public class ScanNode : StatementNode
{
    public List<string> Variables { get; } = new();
}

// IF / ELSE IF / ELSE
public class IfNode : StatementNode
{
    public List<(ExpressionNode Condition, List<StatementNode> Body)> Branches { get; } = new();
    public List<StatementNode>? ElseBranch { get; set; }
}

// FOR (init, condition, update)
public class ForNode : StatementNode
{
    public AssignNode          Initialization { get; set; } = null!;
    public ExpressionNode      Condition      { get; set; } = null!;
    public AssignNode          Update         { get; set; } = null!;
    public List<StatementNode> Body           { get; }      = new();
}

// REPEAT WHEN — spec built-in while loop
// Checks condition BEFORE body. May never run if condition starts false.
// Syntax:
//   REPEAT WHEN (<condition>)
//   START REPEAT
//     <statements>
//   END REPEAT
public class RepeatWhenNode : StatementNode
{
    public ExpressionNode      Condition { get; set; } = null!;
    public List<StatementNode> Body      { get; }      = new();
}

// DO REPEAT UNTIL — our custom do-while loop
// Runs body FIRST then checks condition. Always runs at least once.
// Syntax:
//   DO REPEAT
//   START REPEAT
//     <statements>
//   END REPEAT
//   UNTIL (<condition>)
public class DoRepeatUntilNode : StatementNode
{
    public List<StatementNode> Body      { get; } = new();
    public ExpressionNode      Condition { get; set; } = null!;
}

// Expression nodes
public class IntLiteralNode    : ExpressionNode { public int    Value { get; } public IntLiteralNode(int v)       => Value = v; }
public class FloatLiteralNode  : ExpressionNode { public double Value { get; } public FloatLiteralNode(double v)  => Value = v; }
public class BoolLiteralNode   : ExpressionNode { public bool   Value { get; } public BoolLiteralNode(bool v)    => Value = v; }
public class CharLiteralNode   : ExpressionNode { public char   Value { get; } public CharLiteralNode(char v)    => Value = v; }
public class StringLiteralNode : ExpressionNode { public string Value { get; } public StringLiteralNode(string v) => Value = v; }
public class VariableNode      : ExpressionNode { public string Name  { get; } public VariableNode(string n)     => Name  = n; }

public class BinaryOpNode : ExpressionNode
{
    public ExpressionNode Left  { get; }
    public string         Op    { get; }
    public ExpressionNode Right { get; }
    public BinaryOpNode(ExpressionNode left, string op, ExpressionNode right)
        => (Left, Op, Right) = (left, op, right);
}

public class UnaryOpNode : ExpressionNode
{
    public string         Op      { get; }
    public ExpressionNode Operand { get; }
    public UnaryOpNode(string op, ExpressionNode operand) => (Op, Operand) = (op, operand);
}

public class NewlineNode : ExpressionNode { }  // $ in PRINT

public class EscapeNode : ExpressionNode       // [X] in PRINT
{
    public string Content { get; }
    public EscapeNode(string content) => Content = content;
}