using Microsoft.VisualStudio.TestPlatform.TestHost;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Run {

    public class Program : Module {
        public List<Error> Errors = [];
        public bool HasErrors { get; private set; }
        public Builder Builder;
        public Validator Validator;
        //public Replacer Replacer;
        public Transpiler Transpiler;
        public int LinesCompiled { get; internal set; } = 1;
        public int LinesParsed { get; internal set; }

        Stopwatch Watch = new();
        internal List<string> SearchDirectories = [];
        public HashSet<string> Libraries = [];
        public Dictionary<string, AST> Implicits = [];
        public Dictionary<string, Module> Usings = [];
        public bool HasMain;
        public Main Main;
        internal string ExecutionFolder;
        public Program(string path) : base(path) {
            ExecutionFolder = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory("lib");
        }

        public Program(Stream stream) : base(stream) {
            ExecutionFolder = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory("lib");
        }

        public override void Parse() {
            Watch.Start();
            Program = this;
            Module = this;
            Parent = this;
            if (Scanner == null) {
                Console.Error.WriteLine("Source file not found");
                return;
            }
            Print("Parsing ...", base.Parse);
        }

        public bool PrintErrors() {
            HasErrors |= Errors.Count > 0;
            if (HasErrors) {
                Console.WriteLine();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Errors.ForEach(Console.WriteLine);
                Console.ForegroundColor = ConsoleColor.White;
                Errors.Clear();
            }
            return HasErrors;
        }

        static void GetStartEnd(AST ast, out int start, out int end) {
            end = ast.Scanner.Data.IndexOf('\n', ast.Scanner.Position) - 1;
            start = ast.Scanner.Data.LastIndexOf('\n', ast.Scanner.Position >= ast.Scanner.Data.Length ? ast.Scanner.Data.Length - 1 : ast.Scanner.Position) + 1;
            if (start < 0) {
                start = 0;
            }
            if (end < start) {
                end = ast.Scanner.Data.Length;
            };
        }

        static void GetStartEnd(Token token, out int start, out int end) {
            if (token.Position + (token.Value?.Length ?? 0) >= token.Scanner.Data.Length) {
                token.Position = token.Scanner.Data.Length - 1;
            }
            end = token.Scanner.Data.IndexOf('\n', token.Position) - 1;
            start = token.Scanner.Data.LastIndexOf('\n', token.Position >= token.Scanner.Data.Length ? token.Scanner.Data.Length - 1 : token.Position) + 1;
            if (start < 0) {
                start = 0;
            }
            if (end < start) {
                end = token.Scanner.Data.Length;
            };
        }

        public void AddError(string msg) {
            HasErrors = true;
            Errors.Add(new Error {
                Message = msg,
                Path = "",
                Code = "",
            });
        }

        public void AddError(Token tok, string msg, bool force = false) {
            if (tok == null) return;
            if (Errors.Count > 0) {
                var last = Errors[^1];
                if (force == false && last.Token != null && last.Token.Scanner == tok.Scanner && last.Token.Line == tok.Line) return;
            }
            GetStartEnd(tok, out int start, out int end);
            HasErrors = true;
            Errors.Add(new Error {
                Message = msg,
                Token = tok,
                Path = tok.Scanner.Path,
                Code = tok.Scanner.Data[start..end],
            });
        }

        public void Build(bool includeBuiltin = true) {
            Print("\nBuilding ...", () => (Builder = new(this)).Build(includeBuiltin));
        }

        public void Validate() {
            Print("\nValidating ...", (Validator = new(Builder)).Validate);
            Count();
        }

        public void Replace() {
            Print("\nReplacing ...", (new Replacer(Builder)).Replace);
        }

        public void Count() {
            Print("\nCounting ...", new Counter(Builder).Count);
        }

        public void Transpile() {
            Print("\nTranspiling ...", () => (Transpiler = new C_Transpiler(Builder)).Save(ExecutionFolder + "/" + Path));
        }

        public void PrintResults() {
            if (PrintErrors()) {
                return;
            }
            Console.WriteLine("\nErrors".PadRight(31, '.') + "None");
            LinesParsed = 0;
            foreach (var use in Usings.Values) {
                LinesParsed += use.Scanner.Line;
            }
            Console.WriteLine("Parsed ".PadRight(30, '.') + LinesParsed + " LOC");
            Console.WriteLine("Compiled ".PadRight(30, '.') + LinesCompiled + " LOC");
            Console.WriteLine("Total".PadRight(30, '.') + Watch.ElapsedMilliseconds + " ms");
        }

        public void Compile() {
            Print("\nCompiling ...", () => Transpiler?.Compile());
        }

        void Print(string msg, Action action) {
            if (Scanner == null) {
                return;
            }
            if (HasErrors || Errors.Count > 0) {
                return;
            }
            Console.Out.Flush();
            Console.Write(msg);
            var position = Console.GetCursorPosition();
            Console.WriteLine();
            var sw = Stopwatch.StartNew();
            action?.Invoke();
            if (PrintErrors() == false) {
                PrintOk(position, sw.ElapsedMilliseconds);
            }
        }

        internal static void PrintOk((int Left, int Top) position, long ms = -1) {
            Console.Out.Flush();
            var (Left, Top) = Console.GetCursorPosition();
            Console.SetCursorPosition(position.Left, position.Top);
            Console.WriteLine("".PadRight(30 - position.Left, '.') + "OK" + (ms >= 0 ? " " + ms + " ms" : ""));
            Console.SetCursorPosition(Left, Top);
        }

    }
}
