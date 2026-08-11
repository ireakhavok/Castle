// File: SiegeEngine/Core/UI/JSParser/JSEvaluator.cs
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            // DEFINITIVE: register Math (and common globals) so timeline JS (Math.max/min) and future scripts work
            RegisterGlobal("Math", new Dictionary<object, object>
            {
                ["max"] = new Func<object[], object>(args => args.Length == 0 ? (object)double.NegativeInfinity : (object)args.Max(a => ToNumber(a))),
                ["min"] = new Func<object[], object>(args => args.Length == 0 ? (object)double.PositiveInfinity : (object)args.Min(a => ToNumber(a))),
                ["abs"] = new Func<object, double>(o => Math.Abs(ToNumber(o))),
                ["floor"] = new Func<object, double>(o => Math.Floor(ToNumber(o))),
                ["ceil"] = new Func<object, double>(o => Math.Ceiling(ToNumber(o))),
                ["round"] = new Func<object, double>(o => Math.Round(ToNumber(o))),
                ["random"] = new Func<double>(() => new Random().NextDouble())
            });
            RegisterGlobal("console", new Dictionary<object, object>
            {
                ["log"] = new Action<object>(o => Console.WriteLine("[JS] " + (o?.ToString() ?? "null")))
            });
        }
        public object Evaluate(ASTNode node)
        {
            if (node == null)
            {
                // Handle bare "return;" (no value) and any other null node gracefully
                return null;
            }
            switch (node)
            {
                case ProgramNode program:
                    object last = null;
                    foreach (var stmt in program.Statements)
                    {
                        last = Evaluate(stmt);
                        if (last is ReturnValue) return last;
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
                    object initValue = varDecl.Initializer == null ? null : Evaluate(varDecl.Initializer);
                    CurrentScope()[varDecl.Name] = initValue;
                    return initValue;
                case FunctionDeclarationNode funcDecl:
                    if (funcDecl.Name != null)
                    {
                        // Bind into current scope AND global so both local and recursive lookup work
                        CurrentScope()[funcDecl.Name] = funcDecl;
                        _functions[funcDecl.Name] = funcDecl;
                        _globalScope[funcDecl.Name] = funcDecl;
                    }
                    return funcDecl;
                case ArrowExpressionNode arrow:
                    // Capture the *exact same* Dictionary reference so mutations (let c) are live
                    return new JSArrowClosure(arrow.Params, arrow.Body, CurrentScope(), this);
                case ReturnStatementNode ret:
                    return new ReturnValue(ret.Argument == null ? null : Evaluate(ret.Argument));
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
                    if (forStmt.Init != null) Evaluate(forStmt.Init);
                    object forLast = null;
                    while (forStmt.Test == null || IsTruthy(Evaluate(forStmt.Test)))
                    {
                        forLast = Evaluate(forStmt.Body);
                        if (forLast is ReturnValue)
                            return forLast;
                        if (forStmt.Update != null) Evaluate(forStmt.Update);
                    }
                    return forLast;
                case TryStatementNode tryStmt:
                    object tryResult = null;
                    try
                    {
                        tryResult = Evaluate(tryStmt.Block);
                        if (tryResult is ReturnValue)
                            return tryResult;
                    }
                    catch (JSException jse)
                    {
                        if (tryStmt.CatchBlock != null)
                        {
                            PushScope();
                            if (tryStmt.CatchParam != null)
                            {
                                CurrentScope()[tryStmt.CatchParam] = jse.Value ?? new Dictionary<object, object> { ["message"] = jse.Message };
                            }
                            try
                            {
                                tryResult = Evaluate(tryStmt.CatchBlock);
                            }
                            finally
                            {
                                PopScope();
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (Exception ex) when (!(ex is ReturnException))
                    {
                        // FUTURE-PROOF: any C# exception (parser errors, reflection failures, etc.)
                        // is turned into a JSException so the script's own try/catch can handle it
                        if (tryStmt.CatchBlock != null)
                        {
                            PushScope();
                            if (tryStmt.CatchParam != null)
                            {
                                CurrentScope()[tryStmt.CatchParam] = new Dictionary<object, object>
                                {
                                    ["message"] = ex.Message,
                                    ["name"] = ex.GetType().Name
                                };
                            }
                            try
                            {
                                tryResult = Evaluate(tryStmt.CatchBlock);
                            }
                            finally
                            {
                                PopScope();
                            }
                        }
                        else
                        {
                            throw new JSException(ex.Message);
                        }
                    }
                    finally
                    {
                        if (tryStmt.FinallyBlock != null)
                        {
                            Evaluate(tryStmt.FinallyBlock);
                        }
                    }
                    return tryResult;
                case ThrowStatementNode throwStmt:
                    object throwVal = throwStmt.Argument == null ? null : Evaluate(throwStmt.Argument);
                    throw new JSException(throwVal);
                case BinaryExpressionNode bin:
                    object left = Evaluate(bin.Left);
                    object right = Evaluate(bin.Right);
                    return ApplyBinaryOp(bin.Operator, left, right);
                case UnaryExpressionNode un:
                    object arg = Evaluate(un.Argument);
                    return ApplyUnaryOp(un.Operator, arg);
                case AssignmentExpressionNode assign:
                    object assignRight = Evaluate(assign.Right);
                    // Support compound assignment operators (+ =, -=, etc.) that the parser now emits
                    if (assign.Operator != null && assign.Operator != "=")
                    {
                        object current = Evaluate(assign.Left);
                        assignRight = ApplyBinaryOp(assign.Operator.TrimEnd('='), current, assignRight);
                    }
                    SetValue(assign.Left, assignRight);
                    return assignRight;
                case UpdateExpressionNode update:
                    object updateArg = Evaluate(update.Argument);
                    object newVal;
                    double num = ToNumber(updateArg);
                    if (update.Operator == "++")
                    {
                        newVal = num + 1;
                    }
                    else
                    {
                        newVal = num - 1;
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
                    // === THIS-BINDING for method calls ===
                    object thisArg = null;
                    object callee = null;
                    if (call.Callee is MemberExpressionNode mem)
                    {
                        thisArg = Evaluate(mem.Object);
                        object prop = mem.Computed ? Evaluate(mem.Property) : ((IdentifierNode)mem.Property).Name;
                        callee = GetMember(thisArg, prop);
                    }
                    else
                    {
                        callee = Evaluate(call.Callee);
                    }
                    List<object> args = new List<object>();
                    foreach (var a in call.Arguments)
                    {
                        args.Add(Evaluate(a));
                    }
                    // Temporarily install this for the duration of the call
                    object previousThis = CurrentScope().GetValueOrDefault("this", null);
                    if (thisArg != null)
                    {
                        CurrentScope()["this"] = thisArg;
                    }
                    try
                    {
                        return CallFunction(callee, args);
                    }
                    finally
                    {
                        if (thisArg != null)
                        {
                            if (previousThis == null)
                                CurrentScope().Remove("this");
                            else
                                CurrentScope()["this"] = previousThis;
                        }
                    }
                case IdentifierNode id:
                    return GetVariable(id.Name);
                case LiteralNode lit:
                    return lit.Value;
                case TemplateLiteralNode template:
                    return EvaluateTemplateLiteral(template);
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
                case NewExpressionNode newExpr:
                    // Proper support for "new Error(...)" and "new CustomEvent(...)"
                    object ctor = Evaluate(newExpr.Callee);
                    List<object> newArgs = new List<object>();
                    foreach (var a in newExpr.Arguments)
                    {
                        newArgs.Add(Evaluate(a));
                    }
                    // Detect Error constructor by name
                    string ctorName = null;
                    if (newExpr.Callee is IdentifierNode idn) ctorName = idn.Name;
                    else if (ctor is string s) ctorName = s;
                    if (ctorName == "Error" || (ctor is FunctionDeclarationNode fd && fd.Name == "Error"))
                    {
                        string msg = newArgs.Count > 0 ? (newArgs[0]?.ToString() ?? "") : "";
                        return new Dictionary<object, object>
                        {
                            ["message"] = msg,
                            ["name"] = "Error"
                        };
                    }
                    // Fallback for other constructors
                    return new Dictionary<object, object>();
                default:
                    throw new Exception("Unsupported node type: " + node.GetType());
            }
        }
        private string EvaluateTemplateLiteral(TemplateLiteralNode template)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < template.Quasis.Count; i++)
            {
                result.Append(template.Quasis[i]);
                if (i < template.Expressions.Count)
                {
                    object exprValue = Evaluate(template.Expressions[i]);
                    result.Append(exprValue?.ToString() ?? "");
                }
            }
            return result.ToString();
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
            // Also check the functions table as a last resort for recursive calls
            if (_functions.TryGetValue(name, out var func))
                return func;
            throw new JSException($"Undefined variable: {name}");
        }
        private void SetValue(ASTNode target, object value)
        {
            switch (target)
            {
                case IdentifierNode id:
                    // Walk the scope stack and mutate the *first* dictionary that already owns the name.
                    // This is essential for live closures (the captured Dictionary is one of the scopes).
                    bool set = false;
                    foreach (var scope in _scopeStack)
                    {
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
                // Exact key match first (string keys from Error, plain objects, etc.)
                if (dictObj.TryGetValue(propValue, out object val))
                    return val;
                // Also try stringified form
                if (propValue != null && dictObj.TryGetValue(propValue.ToString(), out val))
                    return val;
                return JSStandardLibrary.GetObjectMember(this, dictObj, propValue?.ToString());
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
            if (objValue is List<JSElement> jsList)
            {
                List<object> boxed = jsList.Cast<object>().ToList();
                if (propValue is double propD && Math.Floor(propD) == propD)
                {
                    int index = (int)propD;
                    if (index >= 0 && index < boxed.Count)
                    {
                        return boxed[index];
                    }
                }
                return JSStandardLibrary.GetArrayMember(this, boxed, propValue.ToString());
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
            if (objValue is JSElement jsElem && propValue is string jsProp)
            {
                if (jsProp == "appendChild")
                {
                    return new Action<JSElement>(child => jsElem.appendChild(child));
                }
                if (jsProp == "value")
                {
                    string tag = jsElem.elem.Tag.ToLower();
                    if (tag == "select")
                    {
                        var selected = jsElem.elem.Children.FirstOrDefault(c => c.Attributes.ContainsKey("selected"));
                        return selected?.Attributes.GetValueOrDefault("value", ((TextElement)selected?.Children.FirstOrDefault())?.Content ?? "") ?? "";
                    }
                    else if (tag == "option")
                    {
                        return jsElem.elem.Attributes.GetValueOrDefault("value", ((TextElement)jsElem.elem.Children.FirstOrDefault())?.Content ?? "");
                    }
                    else if (tag == "input")
                    {
                        if (jsElem.elem is InputElement inp)
                        {
                            return inp.Value ?? "";
                        }
                        return jsElem.elem.Attributes.GetValueOrDefault("value", "");
                    }
                    return "";
                }
                if (jsProp == "innerHTML")
                {
                    return string.Join("", jsElem.elem.Children.OfType<TextElement>().Select(t => t.Content));
                }
                if (jsProp == "textContent")
                {
                    return string.Join("", jsElem.elem.Children.OfType<TextElement>().Select(t => t.Content));
                }
                if (jsProp == "style")
                {
                    return jsElem.style;
                }
                if (jsProp == "classList")
                {
                    return jsElem.classList;
                }
                if (jsProp == "className")
                {
                    return jsElem.className;
                }
                if (jsProp == "preventDefault" || jsProp == "stopPropagation" || jsProp == "stopImmediatePropagation")
                {
                    return new Action(() => { });
                }
                if (jsProp == "getBoundingClientRect")
                {
                    return new Func<Dictionary<object, object>>(() =>
                    {
                        float left = 0, top = 0, width = 100, height = 40;
                        if (jsElem.elem != null)
                        {
                            left = jsElem.elem.ComputedPosition.X;
                            top = jsElem.elem.ComputedPosition.Y;
                            width = jsElem.elem.ComputedWidth > 0 ? jsElem.elem.ComputedWidth : 100;
                            height = jsElem.elem.ComputedHeight > 0 ? jsElem.elem.ComputedHeight : 40;
                        }
                        return new Dictionary<object, object>
                        {
                            ["left"] = left,
                            ["top"] = top,
                            ["width"] = width,
                            ["height"] = height,
                            ["right"] = left + width,
                            ["bottom"] = top + height
                        };
                    });
                }
                if (jsProp == "clientX" || jsProp == "clientY" || jsProp == "pageX" || jsProp == "pageY")
                {
                    return 0.0;
                }
            }
            if (objValue is JSElement.ClassList cls && propValue is string clsProp)
            {
                if (clsProp == "contains")
                    return new Func<string, bool>(cls.contains);
                if (clsProp == "add")
                    return new Action<string>(cls.add);
                if (clsProp == "remove")
                    return new Action<string>(cls.remove);
                if (clsProp == "toggle")
                    return new Action<string>(cls.toggle);
            }
            if (objValue is JSDocument jsDoc && propValue is string docProp && docProp == "createElement")
            {
                return new Func<string, JSElement>(tag => jsDoc.createElement(tag));
            }
            if (propValue is string reflectionProp)
            {
                var type = objValue?.GetType();
                var prop = type?.GetProperty(reflectionProp);
                if (prop != null)
                {
                    return prop.GetValue(objValue);
                }
                var meth = type?.GetMethod(reflectionProp, BindingFlags.Instance | BindingFlags.Public);
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
            if (objValue is JSElement.StyleProxy proxy)
            {
                proxy[propValue.ToString()] = value;
                return;
            }
            if (objValue is JSElement jsElem && propValue is string prop)
            {
                if (prop == "value")
                {
                    string tag = jsElem.elem.Tag.ToLower();
                    if (tag == "select")
                    {
                        foreach (var opt in jsElem.elem.Children.Where(c => c.Tag.ToLower() == "option"))
                        {
                            string optVal = opt.Attributes.GetValueOrDefault("value", ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "");
                            if (optVal == value.ToString())
                                opt.Attributes["selected"] = "";
                            else
                                opt.Attributes.Remove("selected");
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
                            inp.Value = value.ToString();
                        }
                        jsElem.elem.Attributes["value"] = value.ToString();
                    }
                }
                else if (prop == "className")
                {
                    jsElem.className = value?.ToString() ?? "";
                }
                else if (prop == "innerHTML")
                {
                    jsElem.innerHTML = value?.ToString() ?? "";
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
            if (callee is object[] arr && arr.Length == 1)
            {
                callee = arr[0];
            }
            if (callee is JSArrowClosure closure)
            {
                List<object> callArgs = args ?? new List<object>();
                while (callArgs.Count < closure.Params.Count)
                    callArgs.Add(null);
                if (callArgs.Count > closure.Params.Count)
                    callArgs = callArgs.Take(closure.Params.Count).ToList();
                // Push a *new* scope but the Captured dictionary is still the live outer one
                PushScope();
                // Make every variable from the captured scope visible (and writable) through the
                // normal GetVariable / SetValue walk. Because Captured is the *same* Dictionary
                // instance that lives on the outer scope stack, mutations are shared.
                foreach (var kv in closure.Captured)
                {
                    if (!CurrentScope().ContainsKey(kv.Key))
                        CurrentScope()[kv.Key] = kv.Value;
                }
                for (int i = 0; i < callArgs.Count; i++)
                {
                    string paramName = ((IdentifierNode)closure.Params[i]).Name;
                    CurrentScope()[paramName] = callArgs[i];
                }
                object result = null;
                try
                {
                    if (closure.Body is BlockStatementNode block)
                    {
                        foreach (var stmt in block.Body)
                        {
                            result = Evaluate(stmt);
                            if (result is ReturnValue) break;
                        }
                    }
                    else
                    {
                        result = Evaluate(closure.Body);
                    }
                }
                catch (ReturnException re)
                {
                    result = re.Value;
                }
                finally
                {
                    // Write any mutations that happened on the activation record back into the
                    // live captured dictionary so the next invocation of the same closure sees them.
                    foreach (var key in closure.Captured.Keys.ToList())
                    {
                        if (CurrentScope().ContainsKey(key))
                            closure.Captured[key] = CurrentScope()[key];
                    }
                    PopScope();
                }
                return result is ReturnValue rv ? rv.Value : result;
            }
            if (callee is FunctionDeclarationNode func)
            {
                List<object> callArgs = args ?? new List<object>();
                while (callArgs.Count < func.Params.Count)
                    callArgs.Add(null);
                if (callArgs.Count > func.Params.Count)
                    callArgs = callArgs.Take(func.Params.Count).ToList();
                PushScope();
                // Bind the function name into the activation record so recursion works
                if (func.Name != null)
                {
                    CurrentScope()[func.Name] = func;
                }
                for (int i = 0; i < callArgs.Count; i++)
                {
                    CurrentScope()[func.Params[i]] = callArgs[i];
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
                return result is ReturnValue rv ? rv.Value : result;
            }
            // === Universal Delegate path ===
            if (callee is Delegate del)
            {
                var method = del.Method;
                var parameters = method.GetParameters();
                object[] invokeArgs;
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]))
                {
                    invokeArgs = new object[] { (args ?? new List<object>()).ToArray() };
                }
                else
                {
                    var src = args ?? new List<object>();
                    invokeArgs = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        invokeArgs[i] = i < src.Count ? src[i] : null;
                    }
                }
                try
                {
                    return del.DynamicInvoke(invokeArgs);
                }
                catch (TargetParameterCountException)
                {
                    if (invokeArgs.Length == 0 || (invokeArgs.Length == 1 && invokeArgs[0] == null))
                    {
                        return del.DynamicInvoke(Array.Empty<object>());
                    }
                    throw;
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    throw tie.InnerException;
                }
            }
            throw new JSException("Not callable: " + (callee?.GetType().FullName ?? "null"));
        }
        private static double ToNumber(object value)
        {
            if (value == null) return double.NaN;
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is bool b) return b ? 1.0 : 0.0;
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0.0;
                if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
                return double.NaN;
            }
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return double.NaN;
            }
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
            // Arithmetic operators coerce to number (JS semantics)
            if (op == "-" || op == "*" || op == "/" || op == "%")
            {
                double dLeft = ToNumber(left);
                double dRight = ToNumber(right);
                switch (op)
                {
                    case "-": return dLeft - dRight;
                    case "*": return dLeft * dRight;
                    case "/":
                        if (dRight == 0.0) return 0.0;
                        return dLeft / dRight;
                    case "%":
                        if (dRight == 0.0) return 0.0;
                        return dLeft % dRight;
                }
            }
            // + keeps dynamic behaviour so string concatenation continues to work
            dynamic dLeftDyn = left ?? 0;
            dynamic dRightDyn = right ?? 0;
            switch (op)
            {
                case "+": return dLeftDyn + dRightDyn;
                case "==": return dLeftDyn == dRightDyn;
                case "!=": return dLeftDyn != dRightDyn;
                case "<": return dLeftDyn < dRightDyn;
                case ">": return dLeftDyn > dRightDyn;
                case "<=": return dLeftDyn <= dRightDyn;
                case ">=": return dLeftDyn >= dRightDyn;
                case "&": return dLeftDyn & dRightDyn;
                case "|": return dLeftDyn | dRightDyn;
                case "^": return dLeftDyn ^ dRightDyn;
                case "&&": return IsTruthy(left) ? right : left;
                case "||": return IsTruthy(left) ? left : right;
                default: throw new Exception($"Unsupported binary operator: {op}");
            }
        }
        private object ApplyUnaryOp(string op, object arg)
        {
            if (op == "typeof")
            {
                if (arg == null) return "undefined";
                if (arg is bool) return "boolean";
                if (arg is double || arg is float || arg is int || arg is long) return "number";
                if (arg is string) return "string";
                if (arg is FunctionDeclarationNode || arg is JSArrowClosure) return "function";
                return "object";
            }
            if (op == "void")
            {
                return null;
            }
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
            if (value is double d) return d != 0 && !double.IsNaN(d);
            if (value is float f) return f != 0f;
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            if (value is string s) return !string.IsNullOrEmpty(s);
            if (value is List<object> list) return list.Count > 0;
            if (value is Dictionary<object, object> dict) return dict.Count > 0;
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
                Captured = captured; // same reference – live binding
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
    public class JSException : Exception
    {
        public object Value { get; }
        public JSException(object value) : base(value?.ToString() ?? "Error")
        {
            Value = value;
        }
    }
}