using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Run.V12 {

    public class Program : Module {
        public List<Error> Errors = new List<Error>();
        public bool HasErrors { get; private set; }
        public Builder Builder;
        public Validator Validator;
        public Replacer Replacer;

        internal List<string> searchDirectories = new(0);
        public HashSet<string> Libraries = new HashSet<string>(0);
        public Dictionary<string, Module> Usings = new(0);
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
            Program = this;
            Module = this;
            Parent = this;
            if (Scanner == null) {
                Console.Error.WriteLine("Source file not found");
                return;
            }
            if (PrintErrors()) {
                return;
            }
            base.Parse();
            if (PrintErrors() == false) {
                //PrintOk(position);
            }
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

        public void AddError(string msg, AST ast) {
            GetStartEnd(ast, out int start, out int end);
            HasErrors = true;
            Errors.Add(new Error {
                Message = msg,
                Token = ast.Token,
                Path = ast.Scanner.Path,
                Code = ast.Scanner.Data.Substring(start, end - start),
            });
        }

        public void AddError(Token tok, string msg, bool force = false) {
            if (tok == null) return;
            if (Errors.Count > 0) {
                var last = Errors[Errors.Count - 1];
                if (force == false && last.Token.Scanner == tok.Scanner && last.Token.Line == tok.Line) return;
            }
            GetStartEnd(tok, out int start, out int end);
            HasErrors = true;
            Errors.Add(new Error {
                Message = msg,
                Token = tok,
                Path = tok.Scanner.Path,
                Code = tok.Scanner.Data.Substring(start, end - start),
            });
        }

        public void Build() {
            if (Scanner == null) {
                return;
            }
            if (HasErrors || Errors.Count > 0) {
                return;
            }
            Builder = new Builder(this);
            Builder.Build();
            if (PrintErrors() == false) {

            }
        }

        public void Validate() {
            if (Scanner == null) {
                return;
            }
            if (HasErrors || Errors.Count > 0) {
                return;
            }
            Console.Write("  Validating...");
            var position = Console.GetCursorPosition();
            Console.WriteLine();
            Validator = new Validator(Builder);
            Validator.Validate();
            if (PrintErrors() == false) {
                PrintOk(position);
            }
            Replace();
        }

        public void Replace() {
            if (HasErrors || Errors.Count > 0) {
                return;
            }
            Console.Write("  Replacing...");
            var position = Console.GetCursorPosition();
            Console.WriteLine();
            Replacer = new Replacer(Builder);
            Replacer.Replace();
            if (PrintErrors() == false) {
                PrintOk(position);
            }
        }

        internal void PrintOk((int Left, int Top) position) {
            Console.SetCursorPosition(position.Left, position.Top);
            for (int i = position.Left; i < 20; i++) Console.Write('.');
            Console.WriteLine("OK");
        }

        public void Transpile() {
            if (Scanner == null) {
                return;
            }
            if (HasErrors || Errors.Count > 0) {
                return;
            }
            Console.Write("  Counting .........");
            new Counter(Builder).Count();
            Console.WriteLine("OK");
            Console.Write("  Transpiling ...");
            var position = Console.GetCursorPosition();
            Console.WriteLine();
            var transpiler = new Transpiler(Builder);
            if (transpiler.Save(ExecutionFolder + "/" + Path + ".c")) {
                PrintOk(position);
            }
        }
    }
}
