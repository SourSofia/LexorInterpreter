namespace LexorInterpreter;

public enum TokenType
{
    // Program structure
    SCRIPT_AREA,    // SCRIPT AREA
    START_SCRIPT,   // START SCRIPT
    END_SCRIPT,     // END SCRIPT

    // Declaration keywords
    DECLARE,
    INT,
    FLOAT,
    CHAR,
    BOOL,

    // I/O keywords
    PRINT,
    SCAN,

    // Conditional keywords
    IF,
    START_IF,   // START IF
    END_IF,     // END IF
    ELSE,
    ELSE_IF,    // ELSE IF

    // FOR loop keywords
    FOR,
    START_FOR,  // START FOR
    END_FOR,    // END FOR

    // WHILE loop keywords — REPEAT WHEN (spec built-in)
    REPEAT_WHEN,    // REPEAT WHEN
    START_REPEAT,   // START REPEAT
    END_REPEAT,     // END REPEAT

    // DO-WHILE keywords — our custom feature
    DO_REPEAT,  // DO REPEAT
    UNTIL,      // UNTIL

    // Logical operator keywords
    AND,
    OR,
    NOT,

    // Literal token types
    IDENTIFIER,
    INTEGER_LITERAL,
    FLOAT_LITERAL,
    CHAR_LITERAL,
    BOOL_LITERAL,    // "TRUE" or "FALSE"
    STRING_LITERAL,  // any other "..." string

    // Arithmetic operators
    PLUS,       // +
    MINUS,      // -
    MULTIPLY,   // *
    DIVIDE,     // /
    MODULO,     // %

    // Comparison operators
    EQUAL,          // ==
    NOT_EQUAL,      // <>
    LESS,           // <
    GREATER,        // >
    LESS_EQUAL,     // <=
    GREATER_EQUAL,  // >=

    // Assignment
    ASSIGN,  // =

    // Symbols
    LPAREN,     // (
    RPAREN,     // )
    LBRACKET,   // [
    RBRACKET,   // ]
    AMPERSAND,  // &  (concatenator in PRINT)
    DOLLAR,     // $  (newline in PRINT)
    COMMA,      // ,
    COLON,      // :

    // Control
    NEWLINE,
    EOF
}

public class Token
{
    public TokenType Type  { get; }
    public string    Value { get; }
    public int       Line  { get; }

    public Token(TokenType type, string value, int line = 0)
    {
        Type  = type;
        Value = value;
        Line  = line;
    }

    public override string ToString() =>
        $"Token({Type}, \"{Value}\", line {Line})";
}