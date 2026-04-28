namespace LexorInterpreter;

public class Scanner
{
    private readonly string _source;
    private int _pos;
    private int _line;

    // Single-word keywords that map directly to one token type
    private static readonly Dictionary<string, TokenType> SimpleKeywords = new()
    {
        ["DECLARE"] = TokenType.DECLARE,
        ["INT"]     = TokenType.INT,
        ["FLOAT"]   = TokenType.FLOAT,
        ["CHAR"]    = TokenType.CHAR,
        ["BOOL"]    = TokenType.BOOL,
        ["PRINT"]   = TokenType.PRINT,
        ["SCAN"]    = TokenType.SCAN,
        ["IF"]      = TokenType.IF,
        ["ELSE"]    = TokenType.ELSE,
        ["FOR"]     = TokenType.FOR,
        ["AND"]     = TokenType.AND,
        ["OR"]      = TokenType.OR,
        ["NOT"]     = TokenType.NOT,
        ["UNTIL"]   = TokenType.UNTIL,
    };

    public Scanner(string source)
    {
        _source = source;
        _pos    = 0;
        _line   = 1;
    }

    // Entry point
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _source.Length)
        {
            SkipSpacesAndTabs();
            if (_pos >= _source.Length) break;

            char c = Current();

            // %% comment: skip to end of line
            if (c == '%' && PeekChar(1) == '%')
            {
                while (_pos < _source.Length && _source[_pos] != '\n')
                    _pos++;
                continue;
            }

            // Newlines are meaningful tokens in LEXOR
            if (c == '\n')
            {
                tokens.Add(new Token(TokenType.NEWLINE, "\\n", _line));
                _line++;
                _pos++;
                continue;
            }
            if (c == '\r') { _pos++; continue; }

            if (char.IsDigit(c))              { tokens.Add(ReadNumber());            continue; }
            if (char.IsLetter(c) || c == '_') { tokens.Add(ReadWord());              continue; }
            if (c == '\'')                    { tokens.Add(ReadCharLiteral());        continue; }
            if (c == '"')                     { tokens.Add(ReadStringOrBool());       continue; }
            if (c == '[')                     { tokens.AddRange(ReadEscapeBracket()); continue; }

            var sym = ReadSymbol();
            if (sym != null) { tokens.Add(sym); continue; }

            throw new LexorException($"Unexpected character '{c}' at line {_line}");
        }

        tokens.Add(new Token(TokenType.EOF, "", _line));
        return tokens;
    }

    // Character helpers
    private char Current()         => _source[_pos];
    private char PeekChar(int off) => (_pos + off < _source.Length) ? _source[_pos + off] : '\0';
    private void SkipSpacesAndTabs()
    {
        while (_pos < _source.Length &&
               (_source[_pos] == ' ' || _source[_pos] == '\t'))
            _pos++;
    }

    // Reads an integer or float literal
    private Token ReadNumber()
    {
        int start = _pos;
        while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;

        bool isFloat = _pos < _source.Length
                    && _source[_pos] == '.'
                    && char.IsDigit(PeekChar(1));
        if (isFloat)
        {
            _pos++;
            while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;
        }

        string val = _source[start.._pos];
        return new Token(
            isFloat ? TokenType.FLOAT_LITERAL : TokenType.INTEGER_LITERAL,
            val, _line);
    }

    // Reads an identifier or keyword (including multi-word keywords)
    private Token ReadWord()
    {
        int start = _pos;
        while (_pos < _source.Length &&
               (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_'))
            _pos++;

        string word = _source[start.._pos];

        // Multi-word keywords
        if (word == "SCRIPT")
        {
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("AREA"))
                return new Token(TokenType.SCRIPT_AREA, "SCRIPT AREA", _line);
        }

        if (word == "START")
        {
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("SCRIPT")) return new Token(TokenType.START_SCRIPT, "START SCRIPT", _line);
            if (TryConsumeNextWord("IF"))     return new Token(TokenType.START_IF,     "START IF",     _line);
            if (TryConsumeNextWord("FOR"))    return new Token(TokenType.START_FOR,    "START FOR",    _line);
            if (TryConsumeNextWord("REPEAT")) return new Token(TokenType.START_REPEAT, "START REPEAT", _line);
            return new Token(TokenType.IDENTIFIER, "START", _line);
        }

        if (word == "END")
        {
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("SCRIPT")) return new Token(TokenType.END_SCRIPT, "END SCRIPT", _line);
            if (TryConsumeNextWord("IF"))     return new Token(TokenType.END_IF,     "END IF",     _line);
            if (TryConsumeNextWord("FOR"))    return new Token(TokenType.END_FOR,    "END FOR",    _line);
            if (TryConsumeNextWord("REPEAT")) return new Token(TokenType.END_REPEAT, "END REPEAT", _line);
            return new Token(TokenType.IDENTIFIER, "END", _line);
        }

        if (word == "ELSE")
        {
            int saved = _pos;
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("IF")) return new Token(TokenType.ELSE_IF, "ELSE IF", _line);
            _pos = saved;
            return new Token(TokenType.ELSE, "ELSE", _line);
        }

        // REPEAT WHEN  (spec built-in while loop)
        if (word == "REPEAT")
        {
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("WHEN")) return new Token(TokenType.REPEAT_WHEN, "REPEAT WHEN", _line);
            return new Token(TokenType.IDENTIFIER, "REPEAT", _line);
        }

        // DO REPEAT  (our custom do-while feature)
        if (word == "DO")
        {
            SkipSpacesAndTabs();
            if (TryConsumeNextWord("REPEAT")) return new Token(TokenType.DO_REPEAT, "DO REPEAT", _line);
            return new Token(TokenType.IDENTIFIER, "DO", _line);
        }

        // Single-word keywords
        if (SimpleKeywords.TryGetValue(word, out var kwType))
            return new Token(kwType, word, _line);

        // Regular identifier
        return new Token(TokenType.IDENTIFIER, word, _line);
    }

    // Reads next word and returns true if it matches expected; backtracks if not
    private bool TryConsumeNextWord(string expected)
    {
        int saved = _pos;
        int start = _pos;
        while (_pos < _source.Length &&
               (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_'))
            _pos++;
        string next = _source[start.._pos];
        if (next == expected) return true;
        _pos = saved;
        return false;
    }

    // Reads a CHAR literal: 'x'
    private Token ReadCharLiteral()
    {
        _pos++; // skip opening '
        if (_pos >= _source.Length)
            throw new LexorException($"Unterminated char literal at line {_line}");
        char ch = _source[_pos++];
        if (_pos >= _source.Length || _source[_pos] != '\'')
            throw new LexorException($"Char literal must be exactly one character at line {_line}");
        _pos++; // skip closing '
        return new Token(TokenType.CHAR_LITERAL, ch.ToString(), _line);
    }

    // Reads a string or bool literal: "hello" or "TRUE" or "FALSE"
    private Token ReadStringOrBool()
    {
        _pos++; // skip opening "
        int start = _pos;
        while (_pos < _source.Length && _source[_pos] != '"') _pos++;
        if (_pos >= _source.Length)
            throw new LexorException($"Unterminated string at line {_line}");
        string value = _source[start.._pos];
        _pos++; // skip closing "

        return value is "TRUE" or "FALSE"
            ? new Token(TokenType.BOOL_LITERAL,   value, _line)
            : new Token(TokenType.STRING_LITERAL, value, _line);
    }

    // Reads an escape bracket [X]: produces LBRACKET, STRING_LITERAL, RBRACKET
private List<Token> ReadEscapeBracket()
{
    _pos++; // skip opening [
    int start = _pos;

    // Read until the CLOSING ] — but if content itself is ], we need
    // to grab it as a single character (one-char escape like []])
    if (_pos < _source.Length && _source[_pos] == ']')
    {
        // Could be [] (empty) or []] (literal ]) 
        // Peek ahead: if next char after this ] is ], treat first ] as content
        if (_pos + 1 < _source.Length && _source[_pos + 1] == ']')
        {
            // []] → content is "]"
            string content = "]";
            _pos += 2; // skip both ]
            return new List<Token>
            {
                new(TokenType.LBRACKET,       "[",     _line),
                new(TokenType.STRING_LITERAL, content, _line),
                new(TokenType.RBRACKET,       "]",     _line),
            };
        }
        // [] → empty content (treat as empty string)
        _pos++; // skip ]
        return new List<Token>
        {
            new(TokenType.LBRACKET,       "[",  _line),
            new(TokenType.STRING_LITERAL, "",   _line),
            new(TokenType.RBRACKET,       "]",  _line),
        };
    }

    // Normal case: read until ]
    while (_pos < _source.Length && _source[_pos] != ']') _pos++;
    if (_pos >= _source.Length)
        throw new LexorException($"Unclosed escape bracket at line {_line}");
    string val = _source[start.._pos];
    _pos++; // skip closing ]
    return new List<Token>
    {
        new(TokenType.LBRACKET,       "[",  _line),
        new(TokenType.STRING_LITERAL, val,  _line),
        new(TokenType.RBRACKET,       "]",  _line),
    };
}

    // Reads an operator or symbol
    private Token? ReadSymbol()
    {
        char c    = _source[_pos];
        char next = PeekChar(1);

        // Two-character operators
        if (c == '<' && next == '>') { _pos += 2; return new Token(TokenType.NOT_EQUAL,     "<>", _line); }
        if (c == '<' && next == '=') { _pos += 2; return new Token(TokenType.LESS_EQUAL,    "<=", _line); }
        if (c == '>' && next == '=') { _pos += 2; return new Token(TokenType.GREATER_EQUAL, ">=", _line); }
        if (c == '=' && next == '=') { _pos += 2; return new Token(TokenType.EQUAL,         "==", _line); }

        // Single-character operators/symbols
        _pos++;
        return c switch
        {
            '+' => new Token(TokenType.PLUS,      "+", _line),
            '-' => new Token(TokenType.MINUS,     "-", _line),
            '*' => new Token(TokenType.MULTIPLY,  "*", _line),
            '/' => new Token(TokenType.DIVIDE,    "/", _line),
            '%' => new Token(TokenType.MODULO,    "%", _line),
            '<' => new Token(TokenType.LESS,      "<", _line),
            '>' => new Token(TokenType.GREATER,   ">", _line),
            '=' => new Token(TokenType.ASSIGN,    "=", _line),
            '(' => new Token(TokenType.LPAREN,    "(", _line),
            ')' => new Token(TokenType.RPAREN,    ")", _line),
            '&' => new Token(TokenType.AMPERSAND, "&", _line),
            '$' => new Token(TokenType.DOLLAR,    "$", _line),
            ',' => new Token(TokenType.COMMA,     ",", _line),
            ':' => new Token(TokenType.COLON,     ":", _line),
            _   => null
        };
    }
}