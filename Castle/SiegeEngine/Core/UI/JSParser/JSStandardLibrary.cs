// Folder: SiegeEngine.UI/JSParser
// File: JSStandardLibrary.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SiegeEngine.Core.UI.JSParser
{
    public static class JSStandardLibrary
    {
        public static void Register(JSEvaluator evaluator)
        {
            // Global value properties
            evaluator.RegisterGlobal("Infinity", double.PositiveInfinity);
            evaluator.RegisterGlobal("NaN", double.NaN);
            evaluator.RegisterGlobal("undefined", null); // But already throws on undefined

            // Global functions
            evaluator.RegisterGlobal("eval", new Func<string, object>(code =>
            {
                var parser = new JSParser(code);
                var ast = parser.Parse();
                return evaluator.Evaluate(ast);
            }));
            evaluator.RegisterGlobal("isFinite", new Func<object, bool>(o =>
            {
                if (o is double d) return double.IsFinite(d);
                return false;
            }));
            evaluator.RegisterGlobal("isNaN", new Func<object, bool>(o =>
            {
                if (o is double d) return double.IsNaN(d);
                return false;
            }));
            evaluator.RegisterGlobal("parseFloat", new Func<string, double>(s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : double.NaN));
            evaluator.RegisterGlobal("parseInt", new Func<string, int, double>((s, radix) =>
            {
                if (radix < 2 || radix > 36) return double.NaN;
                try
                {
                    return Convert.ToInt32(s, radix);
                }
                catch
                {
                    return double.NaN;
                }
            }));
            evaluator.RegisterGlobal("decodeURI", new Func<string, string>(Uri.UnescapeDataString));
            evaluator.RegisterGlobal("decodeURIComponent", new Func<string, string>(Uri.UnescapeDataString));
            evaluator.RegisterGlobal("encodeURI", new Func<string, string>(Uri.EscapeUriString));
            evaluator.RegisterGlobal("encodeURIComponent", new Func<string, string>(Uri.EscapeDataString));

            // console
            var console = new Dictionary<object, object>();
            console["log"] = new Action<object[]>(args => Console.WriteLine(string.Join(" ", args.Select(a => a?.ToString() ?? ""))));
            // add warn, error, etc. similar
            evaluator.RegisterGlobal("console", console);

            // alert
            evaluator.RegisterGlobal("alert", new Action<object>(o => Console.WriteLine(o?.ToString())));

            // Math
            var math = new Dictionary<object, object>();
            math["E"] = Math.E;
            math["LN10"] = Math.Log(10);
            math["LN2"] = Math.Log(2);
            math["LOG10E"] = 1 / Math.Log(10);
            math["LOG2E"] = 1 / Math.Log(2);
            math["PI"] = Math.PI;
            math["SQRT1_2"] = Math.Sqrt(0.5);
            math["SQRT2"] = Math.Sqrt(2);
            math["abs"] = new Func<double, double>(Math.Abs);
            math["acos"] = new Func<double, double>(Math.Acos);
            math["acosh"] = new Func<double, double>(Math.Acosh);
            math["asin"] = new Func<double, double>(Math.Asin);
            math["asinh"] = new Func<double, double>(Math.Asinh);
            math["atan"] = new Func<double, double>(Math.Atan);
            math["atan2"] = new Func<double, double, double>(Math.Atan2);
            math["atanh"] = new Func<double, double>(Math.Atanh);
            math["cbrt"] = new Func<double, double>(Math.Cbrt);
            math["ceil"] = new Func<double, double>(Math.Ceiling);
            math["clz32"] = new Func<double, double>(d =>
            {
                uint u = (uint)d;
                return u == 0 ? 32 : 31 - (int)Math.Log(u, 2);
            });
            math["cos"] = new Func<double, double>(Math.Cos);
            math["cosh"] = new Func<double, double>(Math.Cosh);
            math["exp"] = new Func<double, double>(Math.Exp);
            math["expm1"] = new Func<double, double>(x => Math.Exp(x) - 1);
            math["exp"] = new Func<double, double>(Math.Exp);
            math["floor"] = new Func<double, double>(Math.Floor);
            math["fround"] = new Func<double, double>(d => (float)d);
            math["hypot"] = new Func<object[], double>(args => Math.Sqrt(args.Sum(a => Math.Pow((double)a, 2))));
            math["imul"] = new Func<double, double, double>((x, y) => (int)x * (int)y);
            math["log"] = new Func<double, double>(Math.Log);
            math["log1p"] = new Func<double, double>(x => Math.Log(1 + x));
            math["log10"] = new Func<double, double>(Math.Log10);
            math["log2"] = new Func<double, double>(Math.Log2);
            math["max"] = new Func<object[], double>(args => args.Any() ? args.Max(a => (double)a) : double.NegativeInfinity);
            math["min"] = new Func<object[], double>(args => args.Any() ? args.Min(a => (double)a) : double.PositiveInfinity);
            math["pow"] = new Func<double, double, double>(Math.Pow);
            math["random"] = new Func<double>(() => new Random().NextDouble());
            math["round"] = new Func<double, double>(Math.Round);
            math["sign"] = new Func<double, double>(x => Math.Sign(x));
            math["sin"] = new Func<double, double>(Math.Sin);
            math["sinh"] = new Func<double, double>(Math.Sinh);
            math["sqrt"] = new Func<double, double>(Math.Sqrt);
            math["tan"] = new Func<double, double>(Math.Tan);
            math["tanh"] = new Func<double, double>(Math.Tanh);
            math["trunc"] = new Func<double, double>(Math.Truncate);
            // add more if needed
            evaluator.RegisterGlobal("Math", math);

            // JSON
            var json = new Dictionary<object, object>();
            json["parse"] = new Func<string, object>(s => JsonSerializer.Deserialize<object>(s));
            json["stringify"] = new Func<object, string>(o => JsonSerializer.Serialize(o));
            evaluator.RegisterGlobal("JSON", json);

            // Date
            evaluator.RegisterGlobal("Date", new Func<object[], object>(args =>
            {
                if (args.Length == 0) return new JSDate();
                if (args.Length == 1)
                {
                    if (args[0] is string strDate)
                    {
                        if (DateTime.TryParse(strDate, out DateTime dt)) return new JSDate(dt);
                        return "Invalid Date";
                    }
                    else if (args[0] is double ms)
                    {
                        return new JSDate(ms);
                    }
                }
                // year, month (0-11), day=1, hour=0, min=0, sec=0, ms=0
                int year = (int)(double)args[0];
                int month = (int)(double)args[1];
                int day = args.Length > 2 ? (int)(double)args[2] : 1;
                int hour = args.Length > 3 ? (int)(double)args[3] : 0;
                int min = args.Length > 4 ? (int)(double)args[4] : 0;
                int sec = args.Length > 5 ? (int)(double)args[5] : 0;
                int ms2 = args.Length > 6 ? (int)(double)args[6] : 0;
                try
                {
                    return new JSDate(new DateTime(year, month + 1, day, hour, min, sec, ms2, DateTimeKind.Utc));
                }
                catch
                {
                    return "Invalid Date";
                }
            }));
            evaluator.RegisterGlobal("Date", new Dictionary<object, object>
            {
                ["now"] = new Func<double>(() => (double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds),
                // parse etc.
            });

            // Add more globals like Object, Array, String constructors

            evaluator.RegisterGlobal("Object", new Func<object[], Dictionary<object, object>>(args => new Dictionary<object, object>()));
            evaluator.RegisterGlobal("Array", new Func<object[], List<object>>(args =>
            {
                if (args.Length == 1 && args[0] is double len) return new List<object>(new object[(int)len]);
                return args.ToList();
            }));
            evaluator.RegisterGlobal("String", new Func<object, string>(o => o?.ToString() ?? ""));
            evaluator.RegisterGlobal("Number", new Func<object, double>(o => o is string s ? double.Parse(s) : (double)o));
            evaluator.RegisterGlobal("Boolean", new Func<object, bool>(o => JSEvaluator.IsTruthy(o)));

            // RegExp
            evaluator.RegisterGlobal("RegExp", new Func<string, string, JSRegex>((pat, flags) => new JSRegex(pat, flags)));

            // Error skip

            // Promise skip no async

            // etc.
        }

        public class JSDate
        {
            public DateTime Date { get; set; }
            public JSDate() { Date = DateTime.UtcNow; }
            public JSDate(double ms) { Date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms); }
            public JSDate(DateTime dt) { Date = dt.ToUniversalTime(); }
            public override string ToString() { return Date.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz '(Coordinated Universal Time)'"); }
        }

        public static object GetDateMember(JSEvaluator eval, JSDate date, string prop)
        {
            switch (prop)
            {
                case "getTime": return (date.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                case "getMilliseconds": return (double)date.Date.Millisecond;
                case "getSeconds": return (double)date.Date.Second;
                case "getMinutes": return (double)date.Date.Minute;
                case "getHours": return (double)date.Date.Hour;
                case "getDate": return (double)date.Date.Day;
                case "getDay": return (double)date.Date.DayOfWeek;
                case "getMonth": return (double)(date.Date.Month - 1);
                case "getFullYear": return (double)date.Date.Year;
                case "getUTCDate": return (double)date.Date.Day;
                // similarly for UTC, since stored as UTC
                // set methods
                case "setMilliseconds": return new Action<double>(ms => date.Date = date.Date.AddMilliseconds(ms - date.Date.Millisecond));
                // add more setSeconds, etc.
                case "toString": return new Func<string>(() => date.ToString());
                // add toUTCString, toISOString, etc.
                default: return null;
            }
        }

        public static object GetRegexMember(JSEvaluator eval, JSRegex regex, string prop)
        {
            var options = GetRegexOptions(regex.Flags);
            switch (prop)
            {
                case "test": return new Func<string, bool>(s => new Regex(regex.Pattern, options).IsMatch(s));
                case "exec":
                    return new Func<string, object>(s =>
                    {
                        var r = new Regex(regex.Pattern, options);
                        var match = r.Match(s);
                        if (!match.Success) return null;
                        var groups = new List<object> { match.Value };
                        for (int i = 1; i < match.Groups.Count; i++)
                        {
                            groups.Add(match.Groups[i].Value);
                        }
                        var result = new Dictionary<object, object>
                        {
                            ["index"] = (double)match.Index,
                            ["input"] = s,
                            ["groups"] = groups // actually groups is named, but simplify
                        };
                        return result;
                    });
                // add source = pattern, flags, lastIndex for sticky, etc.
                default: return null;
            }
        }

        public static object GetStringMember(JSEvaluator eval, string str, string prop)
        {
            switch (prop)
            {
                case "length": return (double)str.Length;
                case "charAt": return new Func<double, string>(index => { int i = (int)index; return i >= 0 && i < str.Length ? str[i].ToString() : ""; });
                case "charCodeAt": return new Func<double, double>(index => { int i = (int)index; return i >= 0 && i < str.Length ? str[i] : double.NaN; });
                case "concat": return new Func<object[], string>(args => str + string.Join("", args.Select(a => a?.ToString() ?? "")));
                case "endsWith": return new Func<string, bool>(search => str.EndsWith(search));
                case "includes": return new Func<string, bool>(search => str.Contains(search));
                case "indexOf": return new Func<string, double>(search => str.IndexOf(search));
                case "lastIndexOf": return new Func<string, double>(search => str.LastIndexOf(search));
                case "match":
                    return new Func<object, object>(pattern =>
                    {
                        Regex r;
                        if (pattern is JSRegex jsr)
                        {
                            r = new Regex(jsr.Pattern, GetRegexOptions(jsr.Flags));
                        }
                        else
                        {
                            r = new Regex(pattern.ToString());
                        }
                        var match = r.Match(str);
                        if (!match.Success) return null;
                        var groups = new List<object> { match.Value };
                        for (int i = 1; i < match.Groups.Count; i++) groups.Add(match.Groups[i].Value);
                        return groups;
                    });
                case "padEnd":
                    return new Func<double, string, string>((targetLength, padString) =>
                    {
                        padString = string.IsNullOrEmpty(padString) ? " " : padString;
                        return str.PadRight((int)targetLength, padString[0]);
                    });
                case "padStart":
                    return new Func<double, string, string>((targetLength, padString) =>
                    {
                        padString = string.IsNullOrEmpty(padString) ? " " : padString;
                        return str.PadLeft((int)targetLength, padString[0]);
                    });
                case "repeat": return new Func<double, string>(count => new string(Enumerable.Repeat(str, (int)count).SelectMany(s => s).ToArray()));
                case "replace":
                    return new Func<object, object, string>((pattern, replacement) =>
                    {
                        string repl = replacement.ToString();
                        Regex r;
                        bool global = false;
                        if (pattern is JSRegex jsr)
                        {
                            r = new Regex(jsr.Pattern, GetRegexOptions(jsr.Flags));
                            global = jsr.Flags.Contains("g");
                        }
                        else
                        {
                            r = new Regex(Regex.Escape(pattern.ToString()));
                        }
                        return global ? r.Replace(str, repl) : r.Replace(str, repl, 1);
                    });
                case "search":
                    return new Func<object, double>(pattern =>
                    {
                        Regex r;
                        if (pattern is JSRegex jsr)
                        {
                            r = new Regex(jsr.Pattern, GetRegexOptions(jsr.Flags));
                        }
                        else
                        {
                            r = new Regex(pattern.ToString());
                        }
                        var match = r.Match(str);
                        return match.Success ? match.Index : -1;
                    });
                case "slice":
                    return new Func<double, double, string>((start, end) =>
                    {
                        int s = (int)start; if (s < 0) s += str.Length;
                        int e = end == double.NaN ? str.Length : (int)end; if (e < 0) e += str.Length;
                        e = Math.Min(e, str.Length); s = Math.Max(s, 0);
                        return str.Substring(s, e - s);
                    });
                case "split": return new Func<string, object>(separator => str.Split(separator).Cast<object>().ToList());
                case "startsWith": return new Func<string, bool>(search => str.StartsWith(search));
                case "substring":
                    return new Func<double, double, string>((start, end) =>
                    {
                        int s = (int)start; int e = end == double.NaN ? str.Length : (int)end;
                        if (s > e) (s, e) = (e, s);
                        return str.Substring(s, e - s);
                    });
                case "toLowerCase": return new Func<string>(() => str.ToLowerInvariant());
                case "toUpperCase": return new Func<string>(() => str.ToUpperInvariant());
                case "trim": return new Func<string>(() => str.Trim());
                // add trimStart, trimEnd, localeCompare, etc.
                default: return null;
            }
        }

        public static object GetNumberMember(JSEvaluator eval, double num, string prop)
        {
            switch (prop)
            {
                case "toExponential": return new Func<double, string>(fractionDigits => num.ToString($"E{(int)fractionDigits}"));
                case "toFixed": return new Func<double, string>(fractionDigits => num.ToString($"F{(int)fractionDigits}"));
                case "toPrecision": return new Func<double, string>(precision => num.ToString($"G{(int)precision}"));
                case "toString": return new Func<double, string>(radix => Convert.ToString((long)num, (int)radix));
                case "valueOf": return new Func<double>(() => num);
                // add more
                default: return null;
            }
        }

        public static object GetObjectMember(JSEvaluator eval, Dictionary<object, object> obj, string prop)
        {
            switch (prop)
            {
                case "hasOwnProperty": return new Func<object, bool>(p => obj.ContainsKey(p));
                case "isPrototypeOf": return new Func<object, bool>(o => false); // no prototype chain
                case "propertyIsEnumerable": return new Func<object, bool>(p => obj.ContainsKey(p)); // simplify
                case "toString": return new Func<string>(() => "[object Object]");
                case "valueOf": return new Func<Dictionary<object, object>>(() => obj);
                case "keys": return new Func<List<object>>(() => obj.Keys.ToList());
                case "values": return new Func<List<object>>(() => obj.Values.ToList());
                case "entries": return new Func<List<List<object>>>(() => obj.Select(kv => new List<object> { kv.Key, kv.Value }).ToList());
                // add assign, create, defineProperty (complex), etc.
                default: return null;
            }
        }

        public static object GetArrayMember(JSEvaluator eval, List<object> arr, string prop)
        {
            switch (prop)
            {
                case "length": return (double)arr.Count;
                case "concat": return new Func<object[], List<object>>(args => arr.Concat(args).ToList());
                case "every": return new Func<object, bool>(callback => arr.All(item => JSEvaluator.IsTruthy(eval.CallFunction(callback, new List<object> { item }))));
                case "filter": return new Func<object, List<object>>(callback => arr.Where(item => JSEvaluator.IsTruthy(eval.CallFunction(callback, new List<object> { item }))).ToList());
                case "find": return new Func<object, object>(callback => arr.FirstOrDefault(item => JSEvaluator.IsTruthy(eval.CallFunction(callback, new List<object> { item }))));
                case "findIndex": return new Func<object, double>(callback => arr.FindIndex(item => JSEvaluator.IsTruthy(eval.CallFunction(callback, new List<object> { item }))));
                case "forEach": return new Action<object>(callback => arr.ForEach(item => eval.CallFunction(callback, new List<object> { item })));
                case "includes": return new Func<object, bool>(value => arr.Contains(value));
                case "indexOf": return new Func<object, double>(value => arr.IndexOf(value));
                case "join": return new Func<string, string>(separator => string.Join(separator, arr.Select(a => a?.ToString() ?? "")));
                case "lastIndexOf": return new Func<object, double>(value => arr.LastIndexOf(value));
                case "map": return new Func<object, List<object>>(callback => arr.Select(item => eval.CallFunction(callback, new List<object> { item })).ToList());
                case "pop": return new Func<object>(() => { if (arr.Count == 0) return null; var last = arr[arr.Count - 1]; arr.RemoveAt(arr.Count - 1); return last; });
                case "push": return new Func<object[], double>(items => { arr.AddRange(items); return arr.Count; });
                case "reduce": return new Func<object, object, object>((callback, initialValue) => arr.Aggregate(initialValue, (acc, item) => eval.CallFunction(callback, new List<object> { acc, item })));
                case "reverse": return new Func<List<object>>(() => { arr.Reverse(); return arr; });
                case "shift": return new Func<object>(() => { if (arr.Count == 0) return null; var first = arr[0]; arr.RemoveAt(0); return first; });
                case "slice": return new Func<double, double, List<object>>((start, end) => { int s = (int)start; int e = end == double.NaN ? arr.Count : (int)end; return arr.GetRange(s, e - s).ToList(); });
                case "some": return new Func<object, bool>(callback => arr.Any(item => JSEvaluator.IsTruthy(eval.CallFunction(callback, new List<object> { item }))));
                case "sort": return new Func<object, List<object>>((compareFn) => { arr.Sort((a, b) => (int)(double)eval.CallFunction(compareFn, new List<object> { a, b })); return arr; });
                case "splice": return new Func<double, double, object[], List<object>>((start, deleteCount, items) => { var removed = arr.GetRange((int)start, (int)deleteCount); arr.RemoveRange((int)start, (int)deleteCount); arr.InsertRange((int)start, items); return removed; });
                case "toString": return new Func<string>(() => string.Join(",", arr));
                case "unshift": return new Func<object[], double>(items => { arr.InsertRange(0, items); return arr.Count; });
                // add more like fill, copyWithin, flat, flatMap, etc.
                default: return null;
            }
        }

        private static RegexOptions GetRegexOptions(string flags)
        {
            RegexOptions options = RegexOptions.None;
            if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
            if (flags.Contains("m")) options |= RegexOptions.Multiline;
            if (flags.Contains("s")) options |= RegexOptions.Singleline;
            if (flags.Contains("u")) options |= RegexOptions.CultureInvariant; // approximate unicode
            // y sticky not supported directly
            return options;
        }
    }
}