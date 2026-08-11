// File: SiegeEngine/Core/UI/JSParser/AstNodes.cs
using System.Collections.Generic;

public abstract class ASTNode
{
}
public class ProgramNode : ASTNode
{
    public List<ASTNode> Statements { get; }
    public ProgramNode(List<ASTNode> statements)
    {
        Statements = statements;
    }
}
public class BlockStatementNode : ASTNode
{
    public List<ASTNode> Body { get; }
    public BlockStatementNode(List<ASTNode> body)
    {
        Body = body;
    }
}
public class ExpressionStatementNode : ASTNode
{
    public ASTNode Expression { get; }
    public ExpressionStatementNode(ASTNode expression)
    {
        Expression = expression;
    }
}
public class VariableDeclarationNode : ASTNode
{
    public string Kind { get; }
    public string Name { get; }
    public ASTNode Initializer { get; }
    public VariableDeclarationNode(string kind, string name, ASTNode initializer)
    {
        Kind = kind;
        Name = name;
        Initializer = initializer;
    }
}
public class FunctionDeclarationNode : ASTNode
{
    public string Name { get; }
    public List<string> Params { get; }
    public ASTNode Body { get; }
    public FunctionDeclarationNode(string name, List<string> paramsList, ASTNode body)
    {
        Name = name;
        Params = paramsList;
        Body = body;
    }
}
public class ArrowExpressionNode : ASTNode
{
    public List<ASTNode> Params { get; }
    public ASTNode Body { get; }
    public ArrowExpressionNode(List<ASTNode> paramsList, ASTNode body)
    {
        Params = paramsList;
        Body = body;
    }
}
public class ReturnStatementNode : ASTNode
{
    public ASTNode Argument { get; }
    public ReturnStatementNode(ASTNode argument)
    {
        Argument = argument;
    }
}
public class IfStatementNode : ASTNode
{
    public ASTNode Test { get; }
    public ASTNode Consequent { get; }
    public ASTNode Alternate { get; }
    public IfStatementNode(ASTNode test, ASTNode consequent, ASTNode alternate)
    {
        Test = test;
        Consequent = consequent;
        Alternate = alternate;
    }
}
public class WhileStatementNode : ASTNode
{
    public ASTNode Test { get; }
    public ASTNode Body { get; }
    public WhileStatementNode(ASTNode test, ASTNode body)
    {
        Test = test;
        Body = body;
    }
}
public class ForStatementNode : ASTNode
{
    public ASTNode Init { get; }
    public ASTNode Test { get; }
    public ASTNode Update { get; }
    public ASTNode Body { get; }
    public ForStatementNode(ASTNode init, ASTNode test, ASTNode update, ASTNode body)
    {
        Init = init;
        Test = test;
        Update = update;
        Body = body;
    }
}
public class BinaryExpressionNode : ASTNode
{
    public ASTNode Left { get; }
    public string Operator { get; }
    public ASTNode Right { get; }
    public BinaryExpressionNode(ASTNode left, string op, ASTNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}
public class UnaryExpressionNode : ASTNode
{
    public string Operator { get; }
    public ASTNode Argument { get; }
    public UnaryExpressionNode(string op, ASTNode argument)
    {
        Operator = op;
        Argument = argument;
    }
}
public class AssignmentExpressionNode : ASTNode
{
    public ASTNode Left { get; }
    public ASTNode Right { get; }
    public AssignmentExpressionNode(ASTNode left, ASTNode right)
    {
        Left = left;
        Right = right;
    }
}
public class UpdateExpressionNode : ASTNode
{
    public string Operator { get; }
    public ASTNode Argument { get; }
    public bool Prefix { get; }
    public UpdateExpressionNode(string op, ASTNode argument, bool prefix)
    {
        Operator = op;
        Argument = argument;
        Prefix = prefix;
    }
}
public class MemberExpressionNode : ASTNode
{
    public ASTNode Object { get; }
    public ASTNode Property { get; }
    public bool Computed { get; }
    public MemberExpressionNode(ASTNode obj, ASTNode prop, bool computed)
    {
        Object = obj;
        Property = prop;
        Computed = computed;
    }
}
public class CallExpressionNode : ASTNode
{
    public ASTNode Callee { get; }
    public List<ASTNode> Arguments { get; }
    public CallExpressionNode(ASTNode callee, List<ASTNode> arguments)
    {
        Callee = callee;
        Arguments = arguments;
    }
}
public class IdentifierNode : ASTNode
{
    public string Name { get; }
    public IdentifierNode(string name)
    {
        Name = name;
    }
}
public class LiteralNode : ASTNode
{
    public object Value { get; }
    public LiteralNode(object value)
    {
        Value = value;
    }
}
public class ArrayExpressionNode : ASTNode
{
    public List<ASTNode> Elements { get; }
    public ArrayExpressionNode(List<ASTNode> elements)
    {
        Elements = elements;
    }
}
public class ObjectExpressionNode : ASTNode
{
    public Dictionary<ASTNode, ASTNode> Properties { get; }
    public ObjectExpressionNode(Dictionary<ASTNode, ASTNode> properties)
    {
        Properties = properties;
    }
}
public class ConditionalExpressionNode : ASTNode
{
    public ASTNode Test { get; }
    public ASTNode Consequent { get; }
    public ASTNode Alternate { get; }
    public ConditionalExpressionNode(ASTNode test, ASTNode consequent, ASTNode alternate)
    {
        Test = test;
        Consequent = consequent;
        Alternate = alternate;
    }
}
public class ThisExpressionNode : ASTNode
{
}
public class JSRegex
{
    public string Pattern { get; }
    public string Flags { get; }
    public JSRegex(string pattern, string flags)
    {
        Pattern = pattern;
        Flags = flags;
    }
}
public class TryStatementNode : ASTNode
{
    public ASTNode Block { get; }
    public string CatchParam { get; }
    public ASTNode CatchBlock { get; }
    public ASTNode FinallyBlock { get; }
    public TryStatementNode(ASTNode block, string catchParam, ASTNode catchBlock, ASTNode finallyBlock)
    {
        Block = block;
        CatchParam = catchParam;
        CatchBlock = catchBlock;
        FinallyBlock = finallyBlock;
    }
}
public class ThrowStatementNode : ASTNode
{
    public ASTNode Argument { get; }
    public ThrowStatementNode(ASTNode argument)
    {
        Argument = argument;
    }
}