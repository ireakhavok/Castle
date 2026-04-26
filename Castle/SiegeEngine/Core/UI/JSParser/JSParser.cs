using System;
using System.Collections.Generic;
using System.Text;
namespace SiegeEngine.Core.UI.JSParser
{
    public class JSParser
    {
        private string _source;
        private int _position;
        private char _currentChar;
        public JSParser(string source)
        {
            _source = source;
            _position = 0;
            Advance();
        }
        private void Advance()
        {
            if (_position < _source.Length)
            {
                _currentChar = _source[_position];
                _position++;
            }
            else
            {
                _currentChar = '\0';
            }
        }
        private char PeekNext()
        {
            return _position < _source.Length ? _source[_position] : '\0';
        }
        private void SkipWhitespaceAndComments()
        {
            while (_currentChar != '\0')
            {
                while (_currentChar != '\0' && char.IsWhiteSpace(_currentChar))
                {
                    Advance();
                }
                if (_currentChar == '/')
                {
                    Advance();
                    if (_currentChar == '/')
                    {
                        SkipSingleLineComment();
                    }
                    else if (_currentChar == '*')
                    {
                        SkipMultiLineComment();
                    }
                    else
                    {
                        _position--;
                        _currentChar = '/';
                        return;
                    }
                }
                else
                {
                    break;
                }
            }
        }
        private Token GetNextToken()
        {
            while (_currentChar != '\0')
            {
                SkipWhitespaceAndComments();
                if (char.IsLetter(_currentChar) || _currentChar == '_' || _currentChar == '$')
                {
                    string id = ParseIdentifier();
                    Token token;
                    if (id == "true") token = new Token(TokenType.True, true);
                    else if (id == "false") token = new Token(TokenType.False, false);
                    else if (id == "null") token = new Token(TokenType.Null, null);
                    else if (id == "this") token = new Token(TokenType.This, null);
                    else if (id == "function") token = new Token(TokenType.Function, null);
                    else token = new Token(TokenType.Identifier, id);
                    return token;
                }
                if (char.IsDigit(_currentChar))
                {
                    Token token = new Token(TokenType.Number, ParseNumber());
                    return token;
                }
                if (_currentChar == '"' || _currentChar == '\'')
                {
                    Token token = new Token(TokenType.String, ParseString());
                    return token;
                }
                if (_currentChar == '`')
                {
                    string content = ParseBacktickString();
                    return new Token(TokenType.String, content);
                }
                if (_currentChar == '/')
                {
                    int savePos = _position;
                    char saveChar = _currentChar;
                    JSRegex regex = ParseRegex();
                    if (regex != null)
                    {
                        Token token = new Token(TokenType.Regex, regex);
                        return token;
                    }
                    _position = savePos;
                    _currentChar = saveChar;
                }
                switch (_currentChar)
                {
                    case '=':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.EqualEqual, "==");
                        }
                        if (_currentChar == '>')
                        {
                            Advance();
                            return new Token(TokenType.Arrow, "=>");
                        }
                        return new Token(TokenType.Assign, "=");
                    case '!':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.NotEqual, "!=");
                        }
                        return new Token(TokenType.Not, "!");
                    case '<':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.LessEqual, "<=");
                        }
                        return new Token(TokenType.Less, "<");
                    case '>':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            return new Token(TokenType.GreaterEqual, ">=");
                        }
                        return new Token(TokenType.Greater, ">");
                    case '+':
                        Advance();
                        if (_currentChar == '+')
                        {
                            Advance();
                            return new Token(TokenType.PlusPlus, "++");
                        }
                        return new Token(TokenType.Plus, "+");
                    case '-':
                        Advance();
                        if (_currentChar == '-')
                        {
                            Advance();
                            return new Token(TokenType.MinusMinus, "--");
                        }
                        return new Token(TokenType.Minus, "-");
                    case '*':
                        Advance();
                        return new Token(TokenType.Multiply, "*");
                    case '/':
                        Advance();
                        return new Token(TokenType.Divide, "/");
                    case '%':
                        Advance();
                        return new Token(TokenType.Modulo, "%");
                    case '(':
                        Advance();
                        return new Token(TokenType.LeftParen, "(");
                    case ')':
                        Advance();
                        return new Token(TokenType.RightParen, ")");
                    case '{':
                        Advance();
                        return new Token(TokenType.LeftBrace, "{");
                    case '}':
                        Advance();
                        return new Token(TokenType.RightBrace, "}");
                    case '[':
                        Advance();
                        return new Token(TokenType.LeftBracket, "[");
                    case ']':
                        Advance();
                        return new Token(TokenType.RightBracket, "]");
                    case ';':
                        Advance();
                        return new Token(TokenType.Semicolon, ";");
                    case ',':
                        Advance();
                        return new Token(TokenType.Comma, ",");
                    case '.':
                        Advance();
                        return new Token(TokenType.Dot, ".");
                    case '?':
                        Advance();
                        return new Token(TokenType.Question, "?");
                    case ':':
                        Advance();
                        return new Token(TokenType.Colon, ":");
                    case '&':
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            return new Token(TokenType.AndAnd, "&&");
                        }
                        return new Token(TokenType.And, "&");
                    case '|':
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            return new Token(TokenType.OrOr, "||");
                        }
                        return new Token(TokenType.Or, "|");
                    case '^':
                        Advance();
                        return new Token(TokenType.Xor, "^");
                    case '~':
                        Advance();
                        return new Token(TokenType.Tilde, "~");
                    default:
                        throw new Exception($"Unexpected character: {_currentChar}");
                }
            }
            return new Token(TokenType.EOF, null);
        }
        private string ParseIdentifier()
        {
            StringBuilder sb = new StringBuilder();
            while (char.IsLetterOrDigit(_currentChar) || _currentChar == '_' || _currentChar == '$')
            {
                sb.Append(_currentChar);
                Advance();
            }
            return sb.ToString();
        }
        private string ParseNumber()
        {
            StringBuilder sb = new StringBuilder();
            while (char.IsDigit(_currentChar) || _currentChar == '.')
            {
                sb.Append(_currentChar);
                Advance();
            }
            return sb.ToString();
        }
        private string ParseString()
        {
            char quote = _currentChar;
            Advance();
            StringBuilder sb = new StringBuilder();
            while (_currentChar != '\0' && _currentChar != quote)
            {
                if (_currentChar == '\\')
                {
                    sb.Append('\\');
                    Advance();
                    if (_currentChar != '\0')
                    {
                        sb.Append(_currentChar);
                        Advance();
                    }
                }
                else
                {
                    sb.Append(_currentChar);
                    Advance();
                }
            }
            if (_currentChar == quote) Advance();
            return sb.ToString();
        }
        private string ParseBacktickString()
        {
            Advance(); // consume opening `
            StringBuilder sb = new StringBuilder();
            while (_currentChar != '\0' && _currentChar != '`')
            {
                if (_currentChar == '\\')
                {
                    sb.Append('\\');
                    Advance();
                    if (_currentChar != '\0')
                    {
                        sb.Append(_currentChar);
                        Advance();
                    }
                }
                else
                {
                    sb.Append(_currentChar);
                    Advance();
                }
            }
            if (_currentChar == '`') Advance();
            return sb.ToString();
        }
        private JSRegex ParseRegex()
        {
            if (_currentChar != '/') return null;
            Advance();
            StringBuilder body = new StringBuilder();
            while (_currentChar != '\0' && _currentChar != '/')
            {
                if (_currentChar == '\\')
                {
                    body.Append('\\');
                    Advance();
                    if (_currentChar != '\0')
                    {
                        body.Append(_currentChar);
                        Advance();
                    }
                }
                else
                {
                    body.Append(_currentChar);
                    Advance();
                }
            }
            if (_currentChar != '/') return null;
            Advance();
            StringBuilder flags = new StringBuilder();
            while (char.IsLetter(_currentChar))
            {
                flags.Append(_currentChar);
                Advance();
            }
            return new JSRegex(body.ToString(), flags.ToString());
        }
        private void SkipSingleLineComment()
        {
            while (_currentChar != '\0' && _currentChar != '\n') Advance();
        }
        private void SkipMultiLineComment()
        {
            Advance();
            while (_currentChar != '\0')
            {
                if (_currentChar == '*' && _position < _source.Length && _source[_position] == '/')
                {
                    Advance();
                    Advance();
                    return;
                }
                Advance();
            }
        }
        public ASTNode Parse()
        {
            return ParseProgram();
        }
        private ASTNode ParseProgram()
        {
            List<ASTNode> statements = new List<ASTNode>();
            while (_currentChar != '\0')
            {
                statements.Add(ParseStatement());
            }
            return new ProgramNode(statements);
        }
        private ASTNode ParseStatement()
        {
            SkipWhitespaceAndComments();
            if (PeekKeyword("var") || PeekKeyword("let") || PeekKeyword("const")) return ParseVariableDeclaration();
            if (PeekKeyword("function")) return ParseFunctionDeclaration();
            if (PeekKeyword("if")) return ParseIfStatement();
            if (PeekKeyword("while")) return ParseWhileStatement();
            if (PeekKeyword("for")) return ParseForStatement();
            if (PeekKeyword("return")) return ParseReturnStatement();
            if (_currentChar == '{') return ParseBlockStatement();
            return ParseExpressionStatement();
        }
        private bool PeekKeyword(string keyword)
        {
            int savePos = _position;
            char saveChar = _currentChar;
            string id = ParseIdentifier();
            _position = savePos;
            _currentChar = saveChar;
            return id == keyword;
        }
        private ASTNode ParseVariableDeclaration()
        {
            string kind = ParseIdentifier();
            SkipWhitespaceAndComments();
            string name = ParseIdentifier();
            SkipWhitespaceAndComments();
            Consume(TokenType.Assign);
            SkipWhitespaceAndComments();
            ASTNode initializer = ParseExpression();
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            return new VariableDeclarationNode(kind, name, initializer);
        }
        private ASTNode ParseFunctionDeclaration()
        {
            ConsumeKeyword("function");
            SkipWhitespaceAndComments();
            string name = ParseIdentifier();
            SkipWhitespaceAndComments();
            Consume(TokenType.LeftParen);
            List<string> paramsList = new List<string>();
            SkipWhitespaceAndComments();
            if (_currentChar != ')')
            {
                paramsList.Add(ParseIdentifier());
                SkipWhitespaceAndComments();
                if (_currentChar == '=')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    ParseExpression(); // consume default value (e.g. = 10.0) but ignore for now
                    SkipWhitespaceAndComments();
                }
                while (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    paramsList.Add(ParseIdentifier());
                    SkipWhitespaceAndComments();
                    if (_currentChar == '=')
                    {
                        Advance();
                        SkipWhitespaceAndComments();
                        ParseExpression();
                        SkipWhitespaceAndComments();
                    }
                }
            }
            Consume(TokenType.RightParen);
            SkipWhitespaceAndComments();
            ASTNode body = ParseBlockStatement();
            return new FunctionDeclarationNode(name, paramsList, body);
        }
        private ASTNode ParseIfStatement()
        {
            ConsumeKeyword("if");
            SkipWhitespaceAndComments();
            Consume(TokenType.LeftParen);
            SkipWhitespaceAndComments();
            ASTNode test = ParseExpression();
            SkipWhitespaceAndComments();
            Consume(TokenType.RightParen);
            SkipWhitespaceAndComments();
            ASTNode consequent = ParseStatement();
            ASTNode alternate = null;
            SkipWhitespaceAndComments();
            if (PeekKeyword("else"))
            {
                ConsumeKeyword("else");
                SkipWhitespaceAndComments();
                alternate = ParseStatement();
            }
            return new IfStatementNode(test, consequent, alternate);
        }
        private ASTNode ParseWhileStatement()
        {
            ConsumeKeyword("while");
            SkipWhitespaceAndComments();
            Consume(TokenType.LeftParen);
            SkipWhitespaceAndComments();
            ASTNode test = ParseExpression();
            SkipWhitespaceAndComments();
            Consume(TokenType.RightParen);
            SkipWhitespaceAndComments();
            ASTNode body = ParseStatement();
            return new WhileStatementNode(test, body);
        }
        private ASTNode ParseForStatement()
        {
            ConsumeKeyword("for");
            SkipWhitespaceAndComments();
            Consume(TokenType.LeftParen);
            SkipWhitespaceAndComments();
            ASTNode init = ParseStatementNoSemi();
            SkipWhitespaceAndComments();
            ASTNode test = ParseExpression();
            SkipWhitespaceAndComments();
            Consume(TokenType.Semicolon);
            SkipWhitespaceAndComments();
            ASTNode update = ParseExpression();
            SkipWhitespaceAndComments();
            Consume(TokenType.RightParen);
            SkipWhitespaceAndComments();
            ASTNode body = ParseStatement();
            return new ForStatementNode(init, test, update, body);
        }
        private ASTNode ParseReturnStatement()
        {
            ConsumeKeyword("return");
            SkipWhitespaceAndComments();
            ASTNode argument = null;
            if (_currentChar != ';' && _currentChar != '}' && _currentChar != '\0')
            {
                argument = ParseExpression();
            }
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            return new ReturnStatementNode(argument);
        }
        private ASTNode ParseBlockStatement()
        {
            Consume(TokenType.LeftBrace);
            SkipWhitespaceAndComments();
            List<ASTNode> body = new List<ASTNode>();
            while (_currentChar != '}')
            {
                body.Add(ParseStatement());
                SkipWhitespaceAndComments();
            }
            Consume(TokenType.RightBrace);
            return new BlockStatementNode(body);
        }
        private ASTNode ParseExpressionStatement()
        {
            ASTNode expr = ParseExpression();
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            return new ExpressionStatementNode(expr);
        }
        private ASTNode ParseStatementNoSemi()
        {
            return ParseVariableDeclarationNoSemi();
        }
        private ASTNode ParseVariableDeclarationNoSemi()
        {
            string kind = ParseIdentifier();
            SkipWhitespaceAndComments();
            string name = ParseIdentifier();
            SkipWhitespaceAndComments();
            Consume(TokenType.Assign);
            SkipWhitespaceAndComments();
            ASTNode initializer = ParseExpression();
            return new VariableDeclarationNode(kind, name, initializer);
        }
        private ASTNode ParseExpression()
        {
            return ParseAssignmentExpression();
        }
        private ASTNode ParseAssignmentExpression()
        {
            ASTNode left = ParseConditionalExpression();
            SkipWhitespaceAndComments();
            if (_currentChar == '=')
            {
                Advance();
                SkipWhitespaceAndComments();
                ASTNode right = ParseAssignmentExpression();
                return new AssignmentExpressionNode(left, right);
            }
            return left;
        }
        private ASTNode ParseConditionalExpression()
        {
            ASTNode test = ParseLogicalOrExpression();
            SkipWhitespaceAndComments();
            if (_currentChar == '?')
            {
                Advance();
                SkipWhitespaceAndComments();
                ASTNode consequent = ParseAssignmentExpression();
                SkipWhitespaceAndComments();
                Consume(TokenType.Colon);
                SkipWhitespaceAndComments();
                ASTNode alternate = ParseAssignmentExpression();
                return new ConditionalExpressionNode(test, consequent, alternate);
            }
            return test;
        }
        private ASTNode ParseLogicalOrExpression()
        {
            ASTNode left = ParseLogicalAndExpression();
            while (Match("||"))
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseLogicalAndExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseLogicalAndExpression()
        {
            ASTNode left = ParseBitwiseOrExpression();
            while (Match("&&"))
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseBitwiseOrExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseBitwiseOrExpression()
        {
            ASTNode left = ParseBitwiseXorExpression();
            while (_currentChar == '|')
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseBitwiseXorExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseBitwiseXorExpression()
        {
            ASTNode left = ParseBitwiseAndExpression();
            while (_currentChar == '^')
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseBitwiseAndExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseBitwiseAndExpression()
        {
            ASTNode left = ParseEqualityExpression();
            while (_currentChar == '&')
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseEqualityExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseEqualityExpression()
        {
            ASTNode left = ParseRelationalExpression();
            while (Match("==") || Match("!="))
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseRelationalExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseRelationalExpression()
        {
            ASTNode left = ParseShiftExpression();
            while (Match("<") || Match(">") || Match("<=") || Match(">="))
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseShiftExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseShiftExpression()
        {
            ASTNode left = ParseAdditiveExpression();
            while (Match("<<") || Match(">>") || Match(">>>"))
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseAdditiveExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseAdditiveExpression()
        {
            ASTNode left = ParseMultiplicativeExpression();
            while (_currentChar == '+' || _currentChar == '-')
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseMultiplicativeExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseMultiplicativeExpression()
        {
            ASTNode left = ParseUnaryExpression();
            while (_currentChar == '*' || _currentChar == '/' || _currentChar == '%')
            {
                string op = GetOperator();
                SkipWhitespaceAndComments();
                ASTNode right = ParseUnaryExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseUnaryExpression()
        {
            if (_currentChar == '!')
            {
                Advance();
                SkipWhitespaceAndComments();
                ASTNode argument = ParseUnaryExpression();
                return new UnaryExpressionNode("!", argument);
            }
            if (_currentChar == '+' || _currentChar == '-' || _currentChar == '~')
            {
                string op = _currentChar.ToString();
                Advance();
                SkipWhitespaceAndComments();
                ASTNode argument = ParseUnaryExpression();
                return new UnaryExpressionNode(op, argument);
            }
            return ParsePostfixExpression();
        }
        private ASTNode ParsePostfixExpression()
        {
            ASTNode left = ParsePrimaryExpression();
            SkipWhitespaceAndComments();
            while (_currentChar == '(' || _currentChar == '[' || _currentChar == '.')
            {
                if (_currentChar == '(')
                {
                    List<ASTNode> args = ParseArguments();
                    left = new CallExpressionNode(left, args);
                }
                else if (_currentChar == '[')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    ASTNode property = ParseExpression();
                    SkipWhitespaceAndComments();
                    Consume(TokenType.RightBracket);
                    left = new MemberExpressionNode(left, property, true);
                }
                else if (_currentChar == '.')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    string property = ParseIdentifier();
                    left = new MemberExpressionNode(left, new IdentifierNode(property), false);
                }
                SkipWhitespaceAndComments();
            }
            if (Match("++") || Match("--"))
            {
                string op = GetOperator();
                return new UpdateExpressionNode(op, left, false);
            }
            return left;
        }
        private List<ASTNode> ParseArguments()
        {
            Consume(TokenType.LeftParen);
            SkipWhitespaceAndComments();
            List<ASTNode> args = new List<ASTNode>();
            if (_currentChar != ')')
            {
                args.Add(ParseAssignmentExpression());
                SkipWhitespaceAndComments();
                while (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    args.Add(ParseAssignmentExpression());
                    SkipWhitespaceAndComments();
                }
            }
            Consume(TokenType.RightParen);
            return args;
        }
        private ASTNode ParsePrimaryExpression()
        {
            Token token = GetNextToken();
            switch (token.Type)
            {
                case TokenType.Function:
                    string name = null;
                    SkipWhitespaceAndComments();
                    if (char.IsLetter(_currentChar) || _currentChar == '_' || _currentChar == '$')
                    {
                        name = ParseIdentifier();
                    }
                    Consume(TokenType.LeftParen);
                    List<string> paramsList = new List<string>();
                    SkipWhitespaceAndComments();
                    if (_currentChar != ')')
                    {
                        paramsList.Add(ParseIdentifier());
                        SkipWhitespaceAndComments();
                        if (_currentChar == '=')
                        {
                            Advance();
                            SkipWhitespaceAndComments();
                            ParseExpression();
                            SkipWhitespaceAndComments();
                        }
                        while (_currentChar == ',')
                        {
                            Advance();
                            SkipWhitespaceAndComments();
                            paramsList.Add(ParseIdentifier());
                            SkipWhitespaceAndComments();
                            if (_currentChar == '=')
                            {
                                Advance();
                                SkipWhitespaceAndComments();
                                ParseExpression();
                                SkipWhitespaceAndComments();
                            }
                        }
                    }
                    Consume(TokenType.RightParen);
                    SkipWhitespaceAndComments();
                    ASTNode body = ParseBlockStatement();
                    return new FunctionDeclarationNode(name, paramsList, body);
                case TokenType.Identifier:
                    string idName = (string)token.Value;
                    SkipWhitespaceAndComments();
                    if (_currentChar == '=' && PeekNext() == '>')
                    {
                        Advance(); // =
                        Advance(); // >
                        SkipWhitespaceAndComments();
                        ASTNode arrowBody;
                        if (_currentChar == '{')
                        {
                            arrowBody = ParseBlockStatement();
                        }
                        else
                        {
                            arrowBody = ParseAssignmentExpression();
                        }
                        return new ArrowExpressionNode(new List<ASTNode> { new IdentifierNode(idName) }, arrowBody);
                    }
                    return new IdentifierNode(idName);
                case TokenType.LeftParen:
                    SkipWhitespaceAndComments();
                    List<ASTNode> paramList = new List<ASTNode>();
                    if (_currentChar != ')')
                    {
                        paramList.Add(ParseExpression());
                        SkipWhitespaceAndComments();
                        while (_currentChar == ',')
                        {
                            Advance();
                            SkipWhitespaceAndComments();
                            paramList.Add(ParseExpression());
                            SkipWhitespaceAndComments();
                        }
                    }
                    Consume(TokenType.RightParen);
                    SkipWhitespaceAndComments();
                    if (_currentChar == '=' && PeekNext() == '>')
                    {
                        Advance(); // =
                        Advance(); // >
                        SkipWhitespaceAndComments();
                        ASTNode arrowBody;
                        if (_currentChar == '{')
                        {
                            arrowBody = ParseBlockStatement();
                        }
                        else
                        {
                            arrowBody = ParseAssignmentExpression();
                        }
                        return new ArrowExpressionNode(paramList, arrowBody);
                    }
                    else
                    {
                        if (paramList.Count == 1)
                        {
                            return paramList[0];
                        }
                        return new BinaryExpressionNode(paramList[0], ",", paramList.Count > 1 ? paramList[1] : null);
                    }
                case TokenType.Number:
                    return new LiteralNode(double.Parse((string)token.Value));
                case TokenType.String:
                    return new LiteralNode((string)token.Value);
                case TokenType.LeftBracket:
                    return ParseArrayLiteral();
                case TokenType.LeftBrace:
                    return ParseObjectLiteral();
                case TokenType.True:
                    return new LiteralNode(true);
                case TokenType.False:
                    return new LiteralNode(false);
                case TokenType.Null:
                    return new LiteralNode(null);
                case TokenType.This:
                    return new ThisExpressionNode();
                case TokenType.Regex:
                    return new LiteralNode(token.Value);
                default:
                    throw new Exception("Unexpected token in primary expression");
            }
        }
        private ASTNode ParseArrayLiteral()
        {
            List<ASTNode> elements = new List<ASTNode>();
            SkipWhitespaceAndComments();
            while (_currentChar != ']')
            {
                if (_currentChar == ',')
                {
                    Advance();
                    elements.Add(null);
                    SkipWhitespaceAndComments();
                    continue;
                }
                elements.Add(ParseAssignmentExpression());
                SkipWhitespaceAndComments();
                if (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                }
            }
            Consume(TokenType.RightBracket);
            return new ArrayExpressionNode(elements);
        }
        private ASTNode ParseObjectLiteral()
        {
            Dictionary<ASTNode, ASTNode> properties = new Dictionary<ASTNode, ASTNode>();
            SkipWhitespaceAndComments();
            while (_currentChar != '}')
            {
                Token keyToken = GetNextToken();
                if (keyToken.Type == TokenType.RightBrace) break;
                ASTNode keyNode;
                if (keyToken.Type == TokenType.Identifier)
                {
                    keyNode = new IdentifierNode((string)keyToken.Value);
                }
                else if (keyToken.Type == TokenType.String)
                {
                    keyNode = new LiteralNode((string)keyToken.Value);
                }
                else if (keyToken.Type == TokenType.Number)
                {
                    keyNode = new LiteralNode(double.Parse((string)keyToken.Value));
                }
                else
                {
                    throw new Exception("Invalid property key");
                }
                SkipWhitespaceAndComments();
                Consume(TokenType.Colon);
                SkipWhitespaceAndComments();
                ASTNode value = ParseAssignmentExpression();
                properties[keyNode] = value;
                SkipWhitespaceAndComments();
                if (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                }
            }
            Consume(TokenType.RightBrace);
            return new ObjectExpressionNode(properties);
        }
        private void Consume(TokenType type)
        {
            Token token = GetNextToken();
            if (token.Type != type)
            {
                throw new Exception($"Expected {type}, got {token.Type}");
            }
        }
        private void ConsumeKeyword(string keyword)
        {
            string id = ParseIdentifier();
            if (id != keyword)
            {
                throw new Exception($"Expected {keyword}, got {id}");
            }
        }
        private bool Match(params string[] ops)
        {
            foreach (var op in ops)
            {
                int start = _position - 1;
                if (start >= 0 && start + op.Length <= _source.Length && _source.Substring(start, op.Length) == op)
                {
                    return true;
                }
            }
            return false;
        }
        private string GetOperator()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_currentChar);
            Advance();
            while (!char.IsLetterOrDigit(_currentChar) && _currentChar != '_' && _currentChar != '$' && _currentChar != '"' && _currentChar != '\'' && _currentChar != '(' && _currentChar != '[' && _currentChar != '{' && _currentChar != '\0' && !char.IsWhiteSpace(_currentChar))
            {
                sb.Append(_currentChar);
                Advance();
            }
            _position--;
            return sb.ToString();
        }
    }
    public enum TokenType
    {
        Identifier,
        Number,
        String,
        Assign,
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulo,
        LeftParen,
        RightParen,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Semicolon,
        Comma,
        Dot,
        Less,
        Greater,
        LessEqual,
        GreaterEqual,
        EqualEqual,
        NotEqual,
        And,
        Or,
        Xor,
        Tilde,
        Not,
        AndAnd,
        OrOr,
        PlusPlus,
        MinusMinus,
        Question,
        Colon,
        Arrow,
        EOF,
        True,
        False,
        Null,
        This,
        Function,
        Regex
    }
    public class Token
    {
        public TokenType Type { get; }
        public object Value { get; }
        public Token(TokenType type, object value)
        {
            Type = type;
            Value = value;
        }
    }
}