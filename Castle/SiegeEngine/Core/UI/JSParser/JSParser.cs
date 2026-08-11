// File: SiegeEngine/Core/UI/JSParser/JSParser.cs
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
        private char Peek(int offset)
        {
            int idx = _position + offset;
            return idx < _source.Length ? _source[idx] : '\0';
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
            if (PeekKeyword("try")) return ParseTryStatement();
            if (PeekKeyword("throw")) return ParseThrowStatement();
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
            ASTNode initializer = null;
            if (_currentChar == '=')
            {
                Advance();
                SkipWhitespaceAndComments();
                initializer = ParseExpression();
            }
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
            ASTNode init = null;
            if (_currentChar != ';')
            {
                if (PeekKeyword("var") || PeekKeyword("let") || PeekKeyword("const"))
                    init = ParseVariableDeclarationNoSemi();
                else
                    init = ParseExpression();
            }
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            SkipWhitespaceAndComments();
            ASTNode test = null;
            if (_currentChar != ';')
            {
                test = ParseExpression();
            }
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            SkipWhitespaceAndComments();
            ASTNode update = null;
            if (_currentChar != ')')
            {
                update = ParseExpression();
            }
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
        private ASTNode ParseTryStatement()
        {
            ConsumeKeyword("try");
            SkipWhitespaceAndComments();
            ASTNode tryBlock = ParseBlockStatement();
            string catchParam = null;
            ASTNode catchBlock = null;
            ASTNode finallyBlock = null;
            SkipWhitespaceAndComments();
            if (PeekKeyword("catch"))
            {
                ConsumeKeyword("catch");
                SkipWhitespaceAndComments();
                if (_currentChar == '(')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    catchParam = ParseIdentifier();
                    SkipWhitespaceAndComments();
                    Consume(TokenType.RightParen);
                }
                SkipWhitespaceAndComments();
                catchBlock = ParseBlockStatement();
            }
            SkipWhitespaceAndComments();
            if (PeekKeyword("finally"))
            {
                ConsumeKeyword("finally");
                SkipWhitespaceAndComments();
                finallyBlock = ParseBlockStatement();
            }
            return new TryStatementNode(tryBlock, catchParam, catchBlock, finallyBlock);
        }
        private ASTNode ParseThrowStatement()
        {
            ConsumeKeyword("throw");
            SkipWhitespaceAndComments();
            ASTNode argument = null;
            if (_currentChar != ';' && _currentChar != '}' && _currentChar != '\0')
            {
                argument = ParseExpression();
            }
            SkipWhitespaceAndComments();
            if (_currentChar == ';') Advance();
            return new ThrowStatementNode(argument);
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
        private ASTNode ParseVariableDeclarationNoSemi()
        {
            string kind = ParseIdentifier();
            SkipWhitespaceAndComments();
            string name = ParseIdentifier();
            SkipWhitespaceAndComments();
            ASTNode initializer = null;
            if (_currentChar == '=')
            {
                Advance();
                SkipWhitespaceAndComments();
                initializer = ParseExpression();
            }
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
            if (_currentChar == '=' ||
                (_currentChar == '+' && PeekNext() == '=') ||
                (_currentChar == '-' && PeekNext() == '=') ||
                (_currentChar == '*' && PeekNext() == '=') ||
                (_currentChar == '/' && PeekNext() == '=') ||
                (_currentChar == '%' && PeekNext() == '='))
            {
                // Do not treat === or !== as assignment
                if (_currentChar == '=' && (PeekNext() == '=' || PeekNext() == '>'))
                {
                    // leave for equality / arrow
                }
                else
                {
                    string op;
                    if (_currentChar == '=')
                    {
                        op = "=";
                        Advance();
                    }
                    else
                    {
                        op = _currentChar + "=";
                        Advance();
                        Advance();
                    }
                    SkipWhitespaceAndComments();
                    ASTNode right = ParseAssignmentExpression();
                    return new AssignmentExpressionNode(left, right, op);
                }
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
                string op = ConsumeOperator("||");
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
                string op = ConsumeOperator("&&");
                SkipWhitespaceAndComments();
                ASTNode right = ParseBitwiseOrExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseBitwiseOrExpression()
        {
            ASTNode left = ParseBitwiseXorExpression();
            while (_currentChar == '|' && PeekNext() != '|')
            {
                string op = ConsumeOperator("|");
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
                string op = ConsumeOperator("^");
                SkipWhitespaceAndComments();
                ASTNode right = ParseBitwiseAndExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseBitwiseAndExpression()
        {
            ASTNode left = ParseEqualityExpression();
            while (_currentChar == '&' && PeekNext() != '&')
            {
                string op = ConsumeOperator("&");
                SkipWhitespaceAndComments();
                ASTNode right = ParseEqualityExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseEqualityExpression()
        {
            ASTNode left = ParseRelationalExpression();
            // Support ===  !==  ==  !=
            while (Match("===") || Match("!==") || Match("==") || Match("!="))
            {
                string op;
                if (Match("===")) op = ConsumeOperator("===");
                else if (Match("!==")) op = ConsumeOperator("!==");
                else if (Match("==")) op = ConsumeOperator("==");
                else op = ConsumeOperator("!=");
                SkipWhitespaceAndComments();
                ASTNode right = ParseRelationalExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseRelationalExpression()
        {
            ASTNode left = ParseShiftExpression();
            while (Match("<=") || Match(">=") || Match("<") || Match(">"))
            {
                string op;
                if (Match("<=")) op = ConsumeOperator("<=");
                else if (Match(">=")) op = ConsumeOperator(">=");
                else if (Match("<")) op = ConsumeOperator("<");
                else op = ConsumeOperator(">");
                SkipWhitespaceAndComments();
                ASTNode right = ParseShiftExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseShiftExpression()
        {
            ASTNode left = ParseAdditiveExpression();
            while (Match("<<") || Match(">>>") || Match(">>"))
            {
                string op;
                if (Match(">>>")) op = ConsumeOperator(">>>");
                else if (Match("<<")) op = ConsumeOperator("<<");
                else op = ConsumeOperator(">>");
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
                if (PeekNext() == '=') break;
                string op = ConsumeOperator(_currentChar.ToString());
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
                if (PeekNext() == '=') break;
                string op = ConsumeOperator(_currentChar.ToString());
                SkipWhitespaceAndComments();
                ASTNode right = ParseUnaryExpression();
                left = new BinaryExpressionNode(left, op, right);
            }
            return left;
        }
        private ASTNode ParseUnaryExpression()
        {
            if (PeekKeyword("typeof"))
            {
                ConsumeKeyword("typeof");
                SkipWhitespaceAndComments();
                ASTNode typeofArg = ParseUnaryExpression();
                return new UnaryExpressionNode("typeof", typeofArg);
            }
            if (PeekKeyword("void"))
            {
                ConsumeKeyword("void");
                SkipWhitespaceAndComments();
                ASTNode voidArg = ParseUnaryExpression();
                return new UnaryExpressionNode("void", voidArg);
            }
            if (_currentChar == '!')
            {
                Advance();
                SkipWhitespaceAndComments();
                ASTNode notArg = ParseUnaryExpression();
                return new UnaryExpressionNode("!", notArg);
            }
            if (_currentChar == '+' || _currentChar == '-' || _currentChar == '~')
            {
                if ((_currentChar == '+' || _currentChar == '-') && PeekNext() == _currentChar)
                {
                    string op = ConsumeOperator(_currentChar.ToString() + _currentChar);
                    SkipWhitespaceAndComments();
                    ASTNode updateArg = ParseUnaryExpression();
                    return new UpdateExpressionNode(op, updateArg, true);
                }
                string uop = _currentChar.ToString();
                Advance();
                SkipWhitespaceAndComments();
                ASTNode unaryArg = ParseUnaryExpression();
                return new UnaryExpressionNode(uop, unaryArg);
            }
            if (PeekKeyword("new"))
            {
                return ParseNewExpression();
            }
            return ParsePostfixExpression();
        }
        private ASTNode ParseNewExpression()
        {
            ConsumeKeyword("new");
            SkipWhitespaceAndComments();
            ASTNode callee = ParsePrimaryExpression();
            SkipWhitespaceAndComments();
            List<ASTNode> args = new List<ASTNode>();
            if (_currentChar == '(')
            {
                args = ParseArguments();
            }
            return new NewExpressionNode(callee, args);
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
                string op = Match("++") ? ConsumeOperator("++") : ConsumeOperator("--");
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
            if (_currentChar == '`')
            {
                return ParseTemplateLiteral();
            }
            if (_currentChar == '\0')
            {
                throw new Exception("Unexpected end of input in primary expression");
            }
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
                        Console.WriteLine("[JSParser] Arrow detected (single param): " + idName);
                        Advance();
                        Advance();
                        SkipWhitespaceAndComments();
                        ASTNode arrowBody = _currentChar == '{' ? ParseBlockStatement() : ParseAssignmentExpression();
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
                        Console.WriteLine("[JSParser] Arrow detected (parenthesized params, count=" + paramList.Count + ")");
                        Advance();
                        Advance();
                        SkipWhitespaceAndComments();
                        ASTNode arrowBody = _currentChar == '{' ? ParseBlockStatement() : ParseAssignmentExpression();
                        return new ArrowExpressionNode(paramList, arrowBody);
                    }
                    else
                    {
                        if (paramList.Count == 1) return paramList[0];
                        return paramList.Count > 0 ? paramList[paramList.Count - 1] : null;
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
                case TokenType.EOF:
                    throw new Exception("Unexpected end of input in primary expression");
                default:
                    string ctx = _position > 20
                        ? _source.Substring(Math.Max(0, _position - 20), Math.Min(40, _source.Length - Math.Max(0, _position - 20)))
                        : _source.Substring(0, Math.Min(40, _source.Length));
                    Console.WriteLine($"[JSParser] Unexpected primary token: Type={token.Type}, Value='{token.Value}', Char='{_currentChar}', Pos={_position}");
                    Console.WriteLine($"[JSParser] Context: ...{ctx}...");
                    throw new Exception($"Unexpected token in primary expression: {token.Type} ('{token.Value}') at pos {_position}");
            }
        }
        private ASTNode ParseTemplateLiteral()
        {
            Advance();
            List<string> quasis = new List<string>();
            List<ASTNode> expressions = new List<ASTNode>();
            StringBuilder currentQuasi = new StringBuilder();
            while (_currentChar != '\0' && _currentChar != '`')
            {
                if (_currentChar == '$' && PeekNext() == '{')
                {
                    quasis.Add(currentQuasi.ToString());
                    currentQuasi.Clear();
                    Advance();
                    Advance();
                    SkipWhitespaceAndComments();
                    expressions.Add(ParseExpression());
                    SkipWhitespaceAndComments();
                    if (_currentChar == '}')
                    {
                        Advance();
                    }
                }
                else
                {
                    currentQuasi.Append(_currentChar);
                    Advance();
                }
            }
            if (_currentChar == '`') Advance();
            quasis.Add(currentQuasi.ToString());
            return new TemplateLiteralNode(quasis, expressions);
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
                    throw new Exception("Invalid property key: " + keyToken.Type);
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
                            if (_currentChar == '=')
                            {
                                Advance();
                                return new Token(TokenType.EqualEqual, "==="); // reuse EqualEqual for === ; evaluator distinguishes by value
                            }
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
                            if (_currentChar == '=')
                            {
                                Advance();
                                return new Token(TokenType.NotEqual, "!==");
                            }
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
                        throw new Exception($"Unexpected character: '{_currentChar}' (code {(int)_currentChar}) at pos {_position}");
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
                if (op.Length == 0) continue;
                if (_currentChar != op[0]) continue;
                bool ok = true;
                for (int i = 1; i < op.Length; i++)
                {
                    if (Peek(i - 1) != op[i])
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return true;
            }
            return false;
        }
        private string ConsumeOperator(string expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (_currentChar != expected[i])
                    throw new Exception($"Expected operator '{expected}', got '{_currentChar}'");
                Advance();
            }
            return expected;
        }
        private string GetOperator()
        {
            if (Match("===")) return ConsumeOperator("===");
            if (Match("!==")) return ConsumeOperator("!==");
            if (Match("==")) return ConsumeOperator("==");
            if (Match("!=")) return ConsumeOperator("!=");
            if (Match("<=")) return ConsumeOperator("<=");
            if (Match(">=")) return ConsumeOperator(">=");
            if (Match("<<")) return ConsumeOperator("<<");
            if (Match(">>>")) return ConsumeOperator(">>>");
            if (Match(">>")) return ConsumeOperator(">>");
            if (Match("&&")) return ConsumeOperator("&&");
            if (Match("||")) return ConsumeOperator("||");
            if (Match("++")) return ConsumeOperator("++");
            if (Match("--")) return ConsumeOperator("--");
            string single = _currentChar.ToString();
            Advance();
            return single;
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
    public class TemplateLiteralNode : ASTNode
    {
        public List<string> Quasis { get; }
        public List<ASTNode> Expressions { get; }
        public TemplateLiteralNode(List<string> quasis, List<ASTNode> expressions)
        {
            Quasis = quasis;
            Expressions = expressions;
        }
    }
    public class NewExpressionNode : ASTNode
    {
        public ASTNode Callee { get; }
        public List<ASTNode> Arguments { get; }
        public NewExpressionNode(ASTNode callee, List<ASTNode> arguments)
        {
            Callee = callee;
            Arguments = arguments;
        }
    }
}