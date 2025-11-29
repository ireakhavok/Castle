// Folder: SiegeEngine.UI/JSParser
// File: JSContext.cs
using System;
using System.Collections.Generic;
namespace SiegeEngine.UI.JSParser
{
    public class JSContext
    {
        public JSEvaluator Evaluator { get; }
        public JSBinding Bindings { get; }
        public JSContext()
        {
            Evaluator = new JSEvaluator();
            Bindings = new JSBinding();
            // Bind built-in functions
            Bindings.Bind("console.log", new Action<object>(o => Console.WriteLine(o)));
            Evaluator.RegisterGlobal("console", new { log = (Action<object>)(o => Console.WriteLine(o)) });
            Evaluator.RegisterGlobal("alert", new Action<object>(o => Console.WriteLine(o)));
        }
        public object Run(string code)
        {
            JSParser parser = new JSParser(code);
            ASTNode ast = parser.Parse();
            return Evaluator.Evaluate(ast);
        }
        public object RunWithThis(string code, object thisObj)
        {
            Console.WriteLine("Debug: Running JS: " + code);
            Evaluator.PushScope();
            Evaluator.CurrentScope()["this"] = thisObj;
            object result = Run(code);
            Evaluator.PopScope();
            return result;
        }
    }
}