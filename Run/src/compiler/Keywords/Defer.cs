using System.IO;

namespace Run {
    public class Defer : Block {
        Expression Expression;
        internal int ID;
        public override void Parse() {
            Token = new Token {
                Scanner = Scanner,
                Column = Scanner.Column,
                Line = Scanner.Line,
                Value = (Parent as Block).Defers.Count.ToString(),
            };
            if (Scanner.Expect('{')) {
                base.Parse();
            } else {
                Expression = ExpressionHelper.Expression(this);
            }
        }
    }
}