// Folder: SiegeEngine.UI/JSParser
// File: JSParser.cs
using System;
using System.Collections.Generic;
using System.Text;
namespace SiegeEngine.UI.JSParser
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
        private void SkipWhitespace()
        {
            while (_currentChar != '\0' && char.IsWhiteSpace(_currentChar))
            {
                Advance();
            }
        }
        private Token GetNextToken()
        {
            while (_currentChar != '\0')
            {
                SkipWhitespace();
                if (char.IsLetter(_currentChar) || _currentChar == '_')
                {
                    var token = new Token(TokenType.Identifier, ParseIdentifier());
                    Console.WriteLine($"Token: {token.Type} - {token.Value}");
                    return token;
                }
                if (char.IsDigit(_currentChar))
                {
                    var token = new Token(TokenType.Number, ParseNumber());
                    Console.WriteLine($"Token: {token.Type} - {token.Value}");
                    return token;
                }
                if (_currentChar == '"')
                {
                    var token = new Token(TokenType.String, ParseString());
                    Console.WriteLine($"Token: {token.Type} - {token.Value}");
                    return token;
                }
                if (_currentChar == '\'')
                {
                    var token = new Token(TokenType.String, ParseString());
                    Console.WriteLine($"Token: {token.Type} - {token.Value}");
                    return token;
                }
                switch (_currentChar)
                {
                    case '=':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            var token = new Token(TokenType.EqualEqual, "==");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        if (_currentChar == '>')
                        {
                            Advance();
                            var token = new Token(TokenType.Arrow, "=>");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var assignToken = new Token(TokenType.Assign, "=");
                        Console.WriteLine($"Token: {assignToken.Type} - {assignToken.Value}");
                        return assignToken;
                    case '!':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            var token = new Token(TokenType.NotEqual, "!=");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var notToken = new Token(TokenType.Not, "!");
                        Console.WriteLine($"Token: {notToken.Type} - {notToken.Value}");
                        return notToken;
                    case '<':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            var token = new Token(TokenType.LessEqual, "<=");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var lessToken = new Token(TokenType.Less, "<");
                        Console.WriteLine($"Token: {lessToken.Type} - {lessToken.Value}");
                        return lessToken;
                    case '>':
                        Advance();
                        if (_currentChar == '=')
                        {
                            Advance();
                            var token = new Token(TokenType.GreaterEqual, ">=");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var greaterToken = new Token(TokenType.Greater, ">");
                        Console.WriteLine($"Token: {greaterToken.Type} - {greaterToken.Value}");
                        return greaterToken;
                    case '+':
                        Advance();
                        if (_currentChar == '+')
                        {
                            Advance();
                            var token = new Token(TokenType.PlusPlus, "++");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var plusToken = new Token(TokenType.Plus, "+");
                        Console.WriteLine($"Token: {plusToken.Type} - {plusToken.Value}");
                        return plusToken;
                    case '-':
                        Advance();
                        if (_currentChar == '-')
                        {
                            Advance();
                            var token = new Token(TokenType.MinusMinus, "--");
                            Console.WriteLine($"Token: {token.Type} - {token.Value}");
                            return token;
                        }
                        var minusToken = new Token(TokenType.Minus, "-");
                        Console.WriteLine($"Token: {minusToken.Type} - {minusToken.Value}");
                        return minusToken;
                    case '*':
                        Advance();
                        var multiplyToken = new Token(TokenType.Multiply, "*");
                        Console.WriteLine($"Token: {multiplyToken.Type} - {multiplyToken.Value}");
                        return multiplyToken;
                    case '/':
                        Advance();
                        if (_currentChar == '/')
                        {
                            SkipSingleLineComment();
                            continue;
                        }
                        if (_currentChar == '*')
                        {
                            SkipMultiLineComment();
                            continue;
                        }
                        var divideToken = new Token(TokenType.Divide, "/");
                        Console.WriteLine($"Token: {divideToken.Type} - {divideToken.Value}");
                        return divideToken;
                    case '%':
                        Advance();
                        var moduloToken = new Token(TokenType.Modulo, "%");
                        Console.WriteLine($"Token: {moduloToken.Type} - {moduloToken.Value}");
                        return moduloToken;
                    case '(':
                        Advance();
                        var leftParenToken = new Token(TokenType.LeftParen, "(");
                        Console.WriteLine($"Token: {leftParenToken.Type} - {leftParenToken.Value}");
                        return leftParenToken;
                    case ')':
                        Advance();
                        var rightParenToken = new Token(TokenType.RightParen, ")");
                        Console.WriteLine($"Token: {rightParenToken.Type} - {rightParenToken.Value}");
                        return rightParenToken;
                    case '{':
                        Advance();
                        var leftBraceToken = new Token(TokenType.LeftBrace, "{");
                        Console.WriteLine($"Token: {leftBraceToken.Type} - {leftBraceToken.Value}");
                        return leftBraceToken;
                    case '}':
                        Advance();
                        var rightBraceToken = new Token(TokenType.RightBrace, "}");
                        Console.WriteLine($"Token: {rightBraceToken.Type} - {rightBraceToken.Value}");
                        return rightBraceToken;
                    case '[':
                        Advance();
                        var leftBracketToken = new Token(TokenType.LeftBracket, "[");
                        Console.WriteLine($"Token: {leftBracketToken.Type} - {leftBracketToken.Value}");
                        return leftBracketToken;
                    case ']':
                        Advance();
                        var rightBracketToken = new Token(TokenType.RightBracket, "]");
                        Console.WriteLine($"Token: {rightBracketToken.Type} - {rightBracketToken.Value}");
                        return rightBracketToken;
                    case ';':
                        Advance();
                        var semicolonToken = new Token(TokenType.Semicolon, ";");
                        Console.WriteLine($"Token: {semicolonToken.Type} - {semicolonToken.Value}");
                        return semicolonToken;
                    case ',':
                        Advance();
                        var commaToken = new Token(TokenType.Comma, ",");
                        Console.WriteLine($"Token: {commaToken.Type} - {commaToken.Value}");
                        return commaToken;
                    case '.':
                        Advance();
                        var dotToken = new Token(TokenType.Dot, ".");
                        Console.WriteLine($"Token: {dotToken.Type} - {dotToken.Value}");
                        return dotToken;
                    case '?':
                        Advance();
                        var questionToken = new Token(TokenType.Question, "?");
                        Console.WriteLine($"Token: {questionToken.Type} - {questionToken.Value}");
                        return questionToken;
                    case ':':
                        Advance();
                        var colonToken = new Token(TokenType.Colon, ":");
                        Console.WriteLine($"Token: {colonToken.Type} - {colonToken.Value}");
                        return colonToken;
                    case '&':
                        Advance();
                        if (_currentChar == '&')
                        {
                            Advance();
                            var andAndToken = new Token(TokenType.AndAnd, "&&");
                            Console.WriteLine($"Token: {andAndToken.Type} - {andAndToken.Value}");
                            return andAndToken;
                        }
                        var andToken = new Token(TokenType.And, "&");
                        Console.WriteLine($"Token: {andToken.Type} - {andToken.Value}");
                        return andToken;
                    case '|':
                        Advance();
                        if (_currentChar == '|')
                        {
                            Advance();
                            var orOrToken = new Token(TokenType.OrOr, "||");
                            Console.WriteLine($"Token: {orOrToken.Type} - {orOrToken.Value}");
                            return orOrToken;
                        }
                        var orToken = new Token(TokenType.Or, "|");
                        Console.WriteLine($"Token: {orToken.Type} - {orToken.Value}");
                        return orToken;
                    case '^':
                        Advance();
                        var xorToken = new Token(TokenType.Xor, "^");
                        Console.WriteLine($"Token: {xorToken.Type} - {xorToken.Value}");
                        return xorToken;
                    case '~':
                        Advance();
                        var tildeToken = new Token(TokenType.Tilde, "~");
                        Console.WriteLine($"Token: {tildeToken.Type} - {tildeToken.Value}");
                        return tildeToken;
                    default:
                        Console.WriteLine($"Unexpected character: {_currentChar}");
                        throw new Exception($"Unexpected character: {_currentChar}");
                }
            }
            var eofToken = new Token(TokenType.EOF, null);
            Console.WriteLine($"Token: {eofToken.Type}");
            return eofToken;
        }
        private string ParseIdentifier()
        {
            StringBuilder sb = new StringBuilder();
            while (char.IsLetterOrDigit(_currentChar) || _currentChar == '_')
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
                sb.Append(_currentChar);
                Advance();
            }
            if (_currentChar == quote)
            {
                Advance();
            }
            return sb.ToString();
        }
        private void SkipSingleLineComment()
        {
            while (_currentChar != '\0' && _currentChar != '\n')
            {
                Advance();
            }
        }
        private void SkipMultiLineComment()
        {
            Advance(); // skip *
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
            SkipWhitespace();
            if (PeekKeyword("var") || PeekKeyword("let") || PeekKeyword("const"))
            {
                return ParseVariableDeclaration();
            }
            if (PeekKeyword("function"))
            {
                return ParseFunctionDeclaration();
            }
            if (PeekKeyword("if"))
            {
                return ParseIfStatement();
            }
            if (PeekKeyword("while"))
            {
                return ParseWhileStatement();
            }
            if (PeekKeyword("for"))
            {
                return ParseForStatement();
            }
            if (PeekKeyword("return"))
            {
                return ParseReturnStatement();
            }
            if (_currentChar == '{')
            {
                return ParseBlockStatement();
            }
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
            string kind = ParseIdentifier(); // var, let, const
            SkipWhitespace();
            string name = ParseIdentifier();
            SkipWhitespace();
            Consume(TokenType.Assign);
            SkipWhitespace();
            ASTNode initializer = ParseExpression();
            SkipWhitespace();
            if (_currentChar == ';')
            {
                Advance();
            }
            return new VariableDeclarationNode(kind, name, initializer);
        }
        private ASTNode ParseFunctionDeclaration()
        {
            ConsumeKeyword("function");
            SkipWhitespace();
            string name = ParseIdentifier();
            SkipWhitespace();
            Consume(TokenType.LeftParen);
            List<string> paramsList = new List<string>();
            SkipWhitespace();
            if (_currentChar != ')')
            {
                paramsList.Add(ParseIdentifier());
                SkipWhitespace();
                while (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespace();
                    paramsList.Add(ParseIdentifier());
                    SkipWhitespace();
                }
            }
            Consume(TokenType.RightParen);
            SkipWhitespace();
            ASTNode body = ParseBlockStatement();
            return new FunctionDeclarationNode(name, paramsList, body);
        }
        private ASTNode ParseIfStatement()
        {
            ConsumeKeyword("if");
            SkipWhitespace();
            Consume(TokenType.LeftParen);
            SkipWhitespace();
            ASTNode test = ParseExpression();
            SkipWhitespace();
            Consume(TokenType.RightParen);
            SkipWhitespace();
            ASTNode consequent = ParseStatement();
            ASTNode alternate = null;
            SkipWhitespace();
            if (PeekKeyword("else"))
            {
                ConsumeKeyword("else");
                SkipWhitespace();
                alternate = ParseStatement();
            }
            return new IfStatementNode(test, consequent, alternate);
        }
        private ASTNode ParseWhileStatement()
        {
            ConsumeKeyword("while");
            SkipWhitespace();
            Consume(TokenType.LeftParen);
            SkipWhitespace();
            ASTNode test = ParseExpression();
            SkipWhitespace();
            Consume(TokenType.RightParen);
            SkipWhitespace();
            ASTNode body = ParseStatement();
            return new WhileStatementNode(test, body);
        }
        private ASTNode ParseForStatement()
        {
            ConsumeKeyword("for");
            SkipWhitespace();
            Consume(TokenType.LeftParen);
            SkipWhitespace();
            ASTNode init = ParseStatementNoSemi();
            SkipWhitespace();
            ASTNode test = ParseExpression();
            SkipWhitespace();
            Consume(TokenType.Semicolon);
            SkipWhitespace();
            ASTNode update = ParseExpression();
            SkipWhitespace();
            Consume(TokenType.RightParen);
            SkipWhitespace();
            ASTNode body = ParseStatement();
            return new ForStatementNode(init, test, update, body);
        }
        private ASTNode ParseReturnStatement()
        {
            ConsumeKeyword("return");
            SkipWhitespace();
            ASTNode argument = ParseExpression();
            SkipWhitespace();
            if (_currentChar == ';')
            {
                Advance();
            }
            return new ReturnStatementNode(argument);
        }
        private ASTNode ParseBlockStatement()
        {
            Consume(TokenType.LeftBrace);
            SkipWhitespace();
            List<ASTNode> body = new List<ASTNode>();
            while (_currentChar != '}')
            {
                body.Add(ParseStatement());
                SkipWhitespace();
            }
            Consume(TokenType.RightBrace);
            return new BlockStatementNode(body);
        }
        private ASTNode ParseExpressionStatement()
        {
            ASTNode expr = ParseExpression();
            SkipWhitespace();
            if (_currentChar == ';')
            {
                Advance();
            }
            return new ExpressionStatementNode(expr);
        }
        private ASTNode ParseStatementNoSemi()
        {
            return ParseVariableDeclarationNoSemi();
        }
        private ASTNode ParseVariableDeclarationNoSemi()
        {
            string kind = ParseIdentifier();
            SkipWhitespace();
            string name = ParseIdentifier();
            SkipWhitespace();
            Consume(TokenType.Assign);
            SkipWhitespace();
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
            SkipWhitespace();
            if (_currentChar == '=')
            {
                Advance();
                SkipWhitespace();
                ASTNode right = ParseAssignmentExpression();
                return new AssignmentExpressionNode(left, right);
            }
            return left;
        }
        private ASTNode ParseConditionalExpression()
        {
            ASTNode test = ParseLogicalOrExpression();
            SkipWhitespace();
            if (_currentChar == '?')
            {
                Advance();
                SkipWhitespace();
                ASTNode consequent = ParseAssignmentExpression();
                SkipWhitespace();
                Consume(TokenType.Colon);
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
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
                SkipWhitespace();
                ASTNode right = ParseUnaryExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseUnaryExpression()
        {
            if (_currentChar == '+' || _currentChar == '-' || _currentChar == '!' || _currentChar == '~')
            {
                string op = GetOperator();
                SkipWhitespace();
                ASTNode argument = ParseUnaryExpression();
                return new UnaryExpressionNode(op, argument);
            }
            return ParsePostfixExpression();
        }
        private ASTNode ParsePostfixExpression()
        {
            ASTNode left = ParsePrimaryExpression();
            SkipWhitespace();
            while (_currentChar == '(' || _currentChar == '[' || _currentChar == '.')
            {
                if (_currentChar == '(')
                {
                    SkipWhitespace();
                    List<ASTNode> args = ParseArguments();
                    left = new CallExpressionNode(left, args);
                }
                else if (_currentChar == '[')
                {
                    Advance();
                    SkipWhitespace();
                    ASTNode property = ParseExpression();
                    SkipWhitespace();
                    Consume(TokenType.RightBracket);
                    left = new MemberExpressionNode(left, property, true);
                }
                else if (_currentChar == '.')
                {
                    Advance();
                    SkipWhitespace();
                    string property = ParseIdentifier();
                    left = new MemberExpressionNode(left, new LiteralNode(property), false);
                }
                SkipWhitespace();
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
            SkipWhitespace();
            List<ASTNode> args = new List<ASTNode>();
            if (_currentChar != ')')
            {
                args.Add(ParseAssignmentExpression());
                SkipWhitespace();
                while (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespace();
                    args.Add(ParseAssignmentExpression());
                    SkipWhitespace();
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
                case TokenType.Identifier:
                    string name = (string)token.Value;
                    SkipWhitespace();
                    if (Match("=>"))
                    {
                        GetOperator(); // consume =>
                        SkipWhitespace();
                        ASTNode body;
                        if (_currentChar == '{')
                        {
                            body = ParseBlockStatement();
                        }
                        else
                        {
                            body = ParseAssignmentExpression();
                        }
                        return new ArrowExpressionNode(new List<ASTNode> { new IdentifierNode(name) }, body);
                    }
                    return new IdentifierNode(name);
                case TokenType.Number:
                    return new LiteralNode(float.Parse((string)token.Value));
                case TokenType.String:
                    return new LiteralNode((string)token.Value);
                case TokenType.LeftParen:
                    ASTNode expr = ParseExpression();
                    SkipWhitespace();
                    Consume(TokenType.RightParen);
                    return expr;
                case TokenType.LeftBracket:
                    return ParseArrayLiteral();
                case TokenType.LeftBrace:
                    return ParseObjectLiteral();
                case TokenType.This:
                    return new ThisExpressionNode();
                default:
                    Console.WriteLine("Unexpected token in primary expression: " + token.Type);
                    throw new Exception("Unexpected token in primary expression: " + token.Type);
            }
        }
        private ASTNode ParseArrayLiteral()
        {
            List<ASTNode> elements = new List<ASTNode>();
            SkipWhitespace();
            while (_currentChar != ']')
            {
                if (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespace();
                    elements.Add(null); // elision
                    continue;
                }
                elements.Add(ParseAssignmentExpression());
                SkipWhitespace();
                if (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespace();
                }
            }
            Consume(TokenType.RightBracket);
            return new ArrayExpressionNode(elements);
        }
        private ASTNode ParseObjectLiteral()
        {
            Dictionary<string, ASTNode> properties = new Dictionary<string, ASTNode>();
            SkipWhitespace();
            while (_currentChar != '}')
            {
                Token keyToken = GetNextToken();
                string key;
                if (keyToken.Type == TokenType.Identifier || keyToken.Type == TokenType.String || keyToken.Type == TokenType.Number)
                {
                    key = keyToken.Value.ToString();
                }
                else
                {
                    Console.WriteLine("Invalid property key: " + keyToken.Type);
                    throw new Exception("Invalid property key");
                }
                SkipWhitespace();
                Consume(TokenType.Colon);
                SkipWhitespace();
                ASTNode value = ParseAssignmentExpression();
                properties[key] = value;
                SkipWhitespace();
                if (_currentChar == ',')
                {
                    Advance();
                    SkipWhitespace();
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
                Console.WriteLine("Expected " + type + ", got " + token.Type);
                throw new Exception("Expected " + type + ", got " + token.Type);
            }
        }
        private void ConsumeKeyword(string keyword)
        {
            string id = ParseIdentifier();
            if (id != keyword)
            {
                Console.WriteLine("Expected " + keyword + ", got " + id);
                throw new Exception("Expected " + keyword + ", got " + id);
            }
        }
        private bool Match(params string[] ops)
        {
            foreach (var op in ops)
            {
                if (_position - op.Length >= 0 && _source.Substring(_position - op.Length, op.Length) == op)
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
            while (!char.IsLetterOrDigit(_currentChar) && _currentChar != '_' && _currentChar != '$' && _currentChar != '"' && _currentChar != '\'' && _currentChar != '(' && _currentChar != '[' && _currentChar != '{' && _currentChar != '\0')
            {
                sb.Append(_currentChar);
                Advance();
            }
            _position--; // back up for next token
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
        This
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