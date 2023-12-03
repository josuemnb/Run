using System;
using System.Collections.Generic;
using System.IO;

namespace Run.V12 {
    public class Namespace : AST {
        public override void Parse() {
            GetName(out Token);
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            }
        }
    }
    public class Using : AST {
        public Token Nick;
        public override void Parse() {
            Token = Scanner.Scan();
            if (Token.Type != TokenType.NAME && Token.Type != TokenType.QUOTE) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                return;
            }
            if (Token.Type == TokenType.QUOTE) {
                Token.Value = Token.Value.Substring(1, Token.Value.Length - 2);
            }
            if (Scanner.IsEOL() == false) {
                if (GetName(out Nick) == false) return;
            }
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                return;
            }
            if (Program.Token.Value == Token.Value || Program.Usings.ContainsKey(Token.Value)) {
                return;
            }
            Nick = new Token {
                Value = Path.GetFileNameWithoutExtension(Token.Value),
            };
            LoadModule(Token.Value);
        }

        public void LoadModule(string path) {
            var use = new Module(path, Parent);
            if (use.Token == null) {
                return;
            }
            var file = Path.GetFileName(use.Path);
            if (Program.Path.Equals(file, StringComparison.InvariantCultureIgnoreCase)) {
                return;
            }
            if (Program.Usings.ContainsKey(file)) {
                return;
            }
            Console.WriteLine("  Parsing '" + path + "'");
            if (use.Valid == false)
                return;
            use.Program = Program;
            use.Using = this;
            use.Nick = Nick?.Value;
            use.Level = 1;
            Program.Usings.Add(file, use);
            use.Parse();
            Program.Children.Insert(0, use);
        }
    }
    public class Module : Block {
        public Using Using;
        public string Path;
        public string Nick;
        internal int Usage;

        public bool Valid => Scanner != null;
        public Module(string path, AST parent = null) {
            if (string.IsNullOrEmpty(System.IO.Path.GetExtension(path))) {
                path += ".run";
            }
            if (path == null || File.Exists(path) == false) {
                if (File.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, path)) == false) {
                    if (parent == null || parent.Scanner != null && File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(parent.Scanner.Address), path)) == false) {
                        parent.Program.AddError(Error.PathNotFound(path));
                        return;
                    } else {
                        path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(parent.Scanner.Address), path);
                        if (File.Exists(path) == false) {
                            parent.Program.AddError(Error.PathNotFound(path));
                            return;
                        }
                    }
                    path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(parent.Scanner.Address), path);
                } else {
                    path = System.IO.Path.Combine(Environment.CurrentDirectory, path);
                }
            }
            Path = path;
            Token = new Token {
                Value = System.IO.Path.GetFileNameWithoutExtension(path),
            };
            Scanner = new Scanner(Path);
            Module = this;
        }

        public Module(Stream stream) {
            Path = "stream.run";
            Token = new Token {
                Value = System.IO.Path.GetFileNameWithoutExtension(Path),
            };
            Scanner = new Scanner(stream);
            Module = this;
        }

        //public override AST FindName(string name) {
        //    if (Token.Value == name) return this;
        //    if (Children != null) {
        //        for (int i = 0; i < Children.Count; i++) {
        //            if (Children[i] is AST a && a.Token != null && a.Token.Value == name) return a;
        //        }
        //    }
        //    return null;
        //}
    }
}
