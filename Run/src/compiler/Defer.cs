using System.IO;

namespace Run {
    public class Defer : Block {
        Expression ExpressionV2;
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
                ExpressionV2 = Add<Expression>();
                ExpressionV2.Parse();
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("__DEFER_STAGE__");
            writer.Write(ID);
            writer.Write(" = ");
            writer.Write(Token.Value);
            writer.WriteLine(";");
        }
    }
}