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
            JSStandardLibrary.Register(Evaluator);
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