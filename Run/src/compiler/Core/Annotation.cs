using System;
using System.IO;

namespace Run {
    public class Annotation : AST {
        public bool IsHeader => Token.Value == "header";
        public bool IsReplace => Token.Value == "replace";
        public string Value;
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.Expect('(')) {
                Value = Scanner.Until(Environment.NewLine)?.Value.Trim();
                if (Value.EndsWith(')')) {
                    Value = Value[..^1];
                }
                Scanner.Scan();
            }
        }

        public override void Print() {
            base.Print();
            if (Value != null) {
                Console.WriteLine(new string(' ', Level * 3) + "[Value] " + Value);
            }
        }
    }
}
