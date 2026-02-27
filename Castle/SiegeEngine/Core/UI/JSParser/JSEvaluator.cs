// Folder: SiegeEngine.Core.UI.JSParser
// File: JSEvaluator.cs
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
namespace SiegeEngine.Core.UI.JSParser
{
    public class JSEvaluator
    {
        private Dictionary<string, object> _globalScope = new Dictionary<string, object>();
        private Stack<Dictionary<string, object>> _scopeStack = new Stack<Dictionary<string, object>>();
        private Dictionary<string, FunctionDeclarationNode> _functions = new Dictionary<string, FunctionDeclarationNode>();
        public JSEvaluator()
        {
            _scopeStack.Push(_globalScope);
        }
        public object Evaluate(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode program:
                    object last = null;
                    foreach (var stmt in program.Statements)
                    {
                        last = Evaluate(stmt);
                    }
                    return last;
                case BlockStatementNode block:
                    PushScope();
                    object blockLast = null;
                    foreach (var stmt in block.Body)
                    {
                        blockLast = Evaluate(stmt);
                        if (blockLast is ReturnValue)
                        {
                            PopScope();
                            return blockLast;
                        }
                    }
                    PopScope();
                    return blockLast;
                case ExpressionStatementNode exprStmt:
                    return Evaluate(exprStmt.Expression);
                case VariableDeclarationNode varDecl:
                    object initValue = Evaluate(varDecl.Initializer);
                    CurrentScope()[varDecl.Name] = initValue;
                    return initValue;
                case FunctionDeclarationNode funcDecl:
                    if (funcDecl.Name != null)
                    {
                        _functions[funcDecl.Name] = funcDecl;
                        _globalScope[funcDecl.Name] = funcDecl;
                    }
                    return funcDecl;
                case ArrowExpressionNode arrow:
                    Console.WriteLine($"[JSEval] Arrow literal evaluated - capturing {CurrentScope().Count} vars (keys: {string.Join(",", CurrentScope().Keys)})");
                    var captured = new Dictionary<string, object>(CurrentScope());
                    return new JSArrowClosure(arrow.Params, arrow.Body, captured, this);
                case ReturnStatementNode ret:
                    return new ReturnValue(Evaluate(ret.Argument));
                case IfStatementNode ifStmt:
                    object test = Evaluate(ifStmt.Test);
                    if (IsTruthy(test))
                    {
                        return Evaluate(ifStmt.Consequent);
                    }
                    else if (ifStmt.Alternate != null)
                    {
                        return Evaluate(ifStmt.Alternate);
                    }
                    return null;
                case WhileStatementNode whileStmt:
                    object whileLast = null;
                    while (IsTruthy(Evaluate(whileStmt.Test)))
                    {
                        whileLast = Evaluate(whileStmt.Body);
                        if (whileLast is ReturnValue)
                            return whileLast;
                    }
                    return whileLast;
                case ForStatementNode forStmt:
                    Evaluate(forStmt.Init);
                    object forLast = null;
                    while (IsTruthy(Evaluate(forStmt.Test)))
                    {
                        forLast = Evaluate(forStmt.Body);
                        if (forLast is ReturnValue)
                            return forLast;
                        Evaluate(forStmt.Update);
                    }
                    return forLast;
                case BinaryExpressionNode bin:
                    object left = Evaluate(bin.Left);
                    object right = Evaluate(bin.Right);
                    return ApplyBinaryOp(bin.Operator, left, right);
                case UnaryExpressionNode un:
                    object arg = Evaluate(un.Argument);
                    return ApplyUnaryOp(un.Operator, arg);
                case AssignmentExpressionNode assign:
                    object assignRight = Evaluate(assign.Right);
                    SetValue(assign.Left, assignRight);
                    return assignRight;
                case UpdateExpressionNode update:
                    object updateArg = Evaluate(update.Argument);
                    object newVal;
                    dynamic dUpdateArg = updateArg;
                    if (update.Operator == "++")
                    {
                        newVal = dUpdateArg + 1;
                    }
                    else
                    {
                        newVal = dUpdateArg - 1;
                    }
                    SetValue(update.Argument, newVal);
                    return update.Prefix ? newVal : updateArg;
                case MemberExpressionNode member:
                    object objValue = Evaluate(member.Object);
                    object propValue;
                    if (member.Computed)
                    {
                        propValue = Evaluate(member.Property);
                    }
                    else
                    {
                        propValue = ((IdentifierNode)member.Property).Name;
                    }
                    return GetMember(objValue, propValue);
                case CallExpressionNode call:
                    object callee = Evaluate(call.Callee);
                    List<object> args = new List<object>();
                    foreach (var a in call.Arguments)
                    {
                        args.Add(Evaluate(a));
                    }
                    return CallFunction(callee, args);
                case IdentifierNode id:
                    return GetVariable(id.Name);
                case LiteralNode lit:
                    return lit.Value;
                case ArrayExpressionNode arr:
                    List<object> arrElements = new List<object>();
                    foreach (var el in arr.Elements)
                    {
                        arrElements.Add(el == null ? null : Evaluate(el));
                    }
                    return arrElements;
                case ObjectExpressionNode objNode:
                    Dictionary<object, object> obj = new Dictionary<object, object>();
                    foreach (var kv in objNode.Properties)
                    {
                        object keyVal;
                        if (kv.Key is IdentifierNode id)
                        {
                            keyVal = id.Name;
                        }
                        else if (kv.Key is LiteralNode lit)
                        {
                            keyVal = lit.Value;
                        }
                        else
                        {
                            throw new Exception("Unsupported key type in object literal");
                        }
                        obj[keyVal] = Evaluate(kv.Value);
                    }
                    return obj;
                case ConditionalExpressionNode cond:
                    object condTest = Evaluate(cond.Test);
                    return IsTruthy(condTest) ? Evaluate(cond.Consequent) : Evaluate(cond.Alternate);
                case ThisExpressionNode _:
                    return CurrentScope().GetValueOrDefault("this", null);
                default:
                    Console.WriteLine("Unsupported node type: " + node.GetType());
                    throw new Exception("Unsupported node type: " + node.GetType());
            }
        }
        public void PushScope()
        {
            _scopeStack.Push(new Dictionary<string, object>());
        }
        public void PopScope()
        {
            _scopeStack.Pop();
        }
        public Dictionary<string, object> CurrentScope()
        {
            return _scopeStack.Peek();
        }
        private object GetVariable(string name)
        {
            foreach (var scope in _scopeStack)
            {
                if (scope.ContainsKey(name))
                {
                    return scope[name];
                }
            }
            throw new Exception($"Undefined variable: {name}");
        }
        private void SetValue(ASTNode target, object value)
        {
            switch (target)
            {
                case IdentifierNode id:
                    bool set = false;
                    for (int i = _scopeStack.Count - 1; i >= 0; i--)
                    {
                        var scope = _scopeStack.ToArray()[i];
                        if (scope.ContainsKey(id.Name))
                        {
                            scope[id.Name] = value;
                            set = true;
                            break;
                        }
                    }
                    if (!set)
                    {
                        CurrentScope()[id.Name] = value;
                    }
                    break;
                case MemberExpressionNode member:
                    object objValue = Evaluate(member.Object);
                    object propValue;
                    if (member.Computed)
                    {
                        propValue = Evaluate(member.Property);
                    }
                    else
                    {
                        propValue = ((IdentifierNode)member.Property).Name;
                    }
                    SetMember(objValue, propValue, value);
                    break;
                default:
                    throw new Exception("Invalid assignment target");
            }
        }
        private object GetMember(object objValue, object propValue)
        {
            if (objValue is Dictionary<object, object> dictObj)
            {
                if (dictObj.TryGetValue(propValue, out object val))
                    return val;
                return JSStandardLibrary.GetObjectMember(this, dictObj, propValue.ToString());
            }
            if (objValue is List<object> listObj)
            {
                if (propValue is double propD && Math.Floor(propD) == propD)
                {
                    int index = (int)propD;
                    if (index >= 0 && index < listObj.Count)
                    {
                        return listObj[index];
                    }
                }
                return JSStandardLibrary.GetArrayMember(this, listObj, propValue.ToString());
            }
            if (objValue is string str)
            {
                return JSStandardLibrary.GetStringMember(this, str, propValue.ToString());
            }
            if (objValue is double num)
            {
                return JSStandardLibrary.GetNumberMember(this, num, propValue.ToString());
            }
            if (objValue is JSRegex regex)
            {
                return JSStandardLibrary.GetRegexMember(this, regex, propValue.ToString());
            }
            if (objValue is JSStandardLibrary.JSDate date)
            {
                return JSStandardLibrary.GetDateMember(this, date, propValue.ToString());
            }
            if (propValue is string propName)
            {
                var type = objValue?.GetType();
                var prop = type?.GetProperty(propName);
                if (prop != null)
                {
                    return prop.GetValue(objValue);
                }
                var meth = type?.GetMethod(propName, BindingFlags.Instance | BindingFlags.Public);
                if (meth != null)
                {
                    return new Func<object[], object>(args => meth.Invoke(objValue, args));
                }
            }
            return null;
        }
        private void SetMember(object objValue, object propValue, object value)
        {
            if (objValue is Dictionary<object, object> dictObj)
            {
                dictObj[propValue] = value;
                return;
            }
            if (objValue is List<object> listObj && propValue is double propD && Math.Floor(propD) == propD)
            {
                int index = (int)propD;
                if (index >= 0 && index < listObj.Count)
                {
                    listObj[index] = value;
                    return;
                }
            }
            if (objValue is JSElement jsElem && propValue is string prop)
            {
                if (prop == "value")
                {
                    string tag = jsElem.elem.Tag.ToLower();
                    string id = jsElem.elem.Attributes.GetValueOrDefault("id", "(no-id)");
                    Console.WriteLine($"[JSElement.value setter] Setting {tag}#{id} value = {value}");
                    string oldValue = "";
                    if (tag == "select")
                    {
                        bool found = false;
                        foreach (var opt in jsElem.elem.Children.Where(c => c.Tag.ToLower() == "option"))
                        {
                            string optVal = opt.Attributes.GetValueOrDefault("value", ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "");
                            if (optVal == value.ToString())
                            {
                                opt.Attributes["selected"] = "";
                                found = true;
                            }
                            else
                            {
                                opt.Attributes.Remove("selected");
                            }
                        }
                        if (found)
                        {
                            jsElem.overlay.RefreshUI();
                            jsElem.overlay.TriggerChange(jsElem.elem);
                        }
                    }
                    else if (tag == "option")
                    {
                        jsElem.elem.Attributes["value"] = value.ToString();
                    }
                    else if (tag == "input")
                    {
                        if (jsElem.elem is InputElement inp)
                        {
                            oldValue = inp.Value;
                            inp.Value = value.ToString();
                        }
                        jsElem.elem.Attributes["value"] = value.ToString();
                        if (oldValue != value.ToString())
                        {
                            Console.WriteLine($"[JSElement] Triggering input listeners after C# value set on {tag}#{id}");
                            jsElem.overlay.RefreshUI();
                            jsElem.overlay.InvokeListeners(jsElem.elem, "input");
                            jsElem.overlay.TriggerChange(jsElem.elem);
                        }
                    }
                }
                else if (prop == "innerHTML")
                {
                    Console.WriteLine("Debug: Setting innerHTML");
                    if (value is string strVal && strVal == "")
                    {
                        jsElem.elem.Children.Clear();
                        jsElem.overlay.RefreshUI();
                    }
                }
                else if (prop == "textContent")
                {
                    if (value is string txt)
                    {
                        jsElem.elem.Children.RemoveAll(c => c is TextElement);
                        if (!string.IsNullOrEmpty(txt))
                        {
                            TextElement textElem = new TextElement { Content = txt };
                            textElem.Parent = jsElem.elem;
                            jsElem.elem.Children.Add(textElem);
                        }
                        jsElem.overlay.RefreshUI();
                    }
                }
                else if (prop == "style")
                {
                    if (value is Dictionary<object, object> styleDict)
                    {
                        foreach (var kv in styleDict)
                        {
                            if (kv.Key is string key && kv.Value is string val)
                            {
                                jsElem.elem.Style.SetProperty(key, val);
                            }
                        }
                        jsElem.overlay.RefreshUI();
                    }
                }
                return;
            }
            var type = objValue?.GetType();
            var prop1 = type?.GetProperty(propValue.ToString());
            prop1?.SetValue(objValue, value);
        }
        public object CallFunction(object callee, List<object> args)
        {
            if (callee is FunctionDeclarationNode func)
            {
                Console.WriteLine("Debug: Calling function " + func.Name ?? "anonymous");
                if (func.Params.Count != args.Count)
                {
                    throw new Exception("Argument count mismatch");
                }
                PushScope();
                for (int i = 0; i < args.Count; i++)
                {
                    CurrentScope()[func.Params[i]] = args[i];
                }
                object result = null;
                try
                {
                    result = Evaluate(func.Body);
                }
                catch (ReturnException re)
                {
                    result = re.Value;
                }
                finally
                {
                    PopScope();
                }
                return result;
            }
            if (callee is JSArrowClosure closure)
            {
                Console.WriteLine($"[ArrowCall] Invoking closure - captured keys: {string.Join(",", closure.Captured.Keys)}  args.Count={args.Count}");
                if (closure.Params.Count != args.Count)
                {
                    throw new Exception("Argument count mismatch");
                }
                PushScope();
                foreach (var kv in closure.Captured)
                {
                    if (!CurrentScope().ContainsKey(kv.Key))
                    {
                        CurrentScope()[kv.Key] = kv.Value;
                        Console.WriteLine($"[ArrowCall] Restored captured var '{kv.Key}' = {kv.Value}");
                    }
                }
                for (int i = 0; i < args.Count; i++)
                {
                    string paramName = ((IdentifierNode)closure.Params[i]).Name;
                    CurrentScope()[paramName] = args[i];
                }
                object result = null;
                try
                {
                    result = closure.Evaluator.Evaluate(closure.Body);
                }
                catch (ReturnException re)
                {
                    result = re.Value;
                }
                finally
                {
                    PopScope();
                }
                return result;
            }
            if (callee is Func<object[], object> funcObj)
            {
                return funcObj(args.ToArray());
            }
            if (callee is Action<object> act)
            {
                act.DynamicInvoke(args[0]);
                return null;
            }
            if (callee is Delegate del)
            {
                return del.DynamicInvoke(args.ToArray());
            }
            throw new Exception("Not callable");
        }
        private object ApplyBinaryOp(string op, object left, object right)
        {
            if (op == "===")
            {
                if (left == null && right == null) return true;
                if (left == null || right == null) return false;
                if (left.GetType() != right.GetType()) return false;
                return left.Equals(right);
            }
            if (op == "!==")
            {
                return !(bool)ApplyBinaryOp("===", left, right);
            }
            dynamic dLeft = left ?? 0;
            dynamic dRight = right ?? 0;
            switch (op)
            {
                case "+": return dLeft + dRight;
                case "-": return dLeft - dRight;
                case "*": return dLeft * dRight;
                case "/": return dLeft / dRight;
                case "%": return dLeft % dRight;
                case "==": return dLeft == dRight;
                case "!=": return dLeft != dRight;
                case "<": return dLeft < dRight;
                case ">": return dLeft > dRight;
                case "<=": return dLeft <= dRight;
                case ">=": return dLeft >= dRight;
                case "&": return dLeft & dRight;
                case "|": return dLeft | dRight;
                case "^": return dLeft ^ dRight;
                case "&&": return IsTruthy(left) ? right : left;
                case "||": return IsTruthy(left) ? left : right;
                default: throw new Exception($"Unsupported binary operator: {op}");
            }
        }
        private object ApplyUnaryOp(string op, object arg)
        {
            dynamic dArg = arg ?? 0;
            switch (op)
            {
                case "+": return +dArg;
                case "-": return -dArg;
                case "!": return !IsTruthy(arg);
                case "~": return ~dArg;
                default: throw new Exception($"Unsupported unary operator: {op}");
            }
        }
        public static bool IsTruthy(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is float f) return f != 0f;
            if (value is string s) return !string.IsNullOrEmpty(s);
            if (value is List<object> l) return l.Count > 0;
            if (value is Dictionary<object, object> d) return d.Count > 0;
            return true;
        }
        public void RegisterFunction(string name, FunctionDeclarationNode func)
        {
            _functions[name] = func;
        }
        public void RegisterGlobal(string name, object value)
        {
            _globalScope[name] = value;
        }

        private class JSArrowClosure
        {
            public List<ASTNode> Params { get; }
            public ASTNode Body { get; }
            public Dictionary<string, object> Captured { get; }
            public JSEvaluator Evaluator { get; }

            public JSArrowClosure(List<ASTNode> paramsList, ASTNode body, Dictionary<string, object> captured, JSEvaluator evaluator)
            {
                Params = paramsList;
                Body = body;
                Captured = captured;
                Evaluator = evaluator;
            }
        }
    }
    public class ReturnValue
    {
        public object Value { get; }
        public ReturnValue(object value)
        {
            Value = value;
        }
    }
    public class ReturnException : Exception
    {
        public object Value { get; }
        public ReturnException(object value) : base("Return")
        {
            Value = value;
        }
    }
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
}