using System.IO;

namespace Run {
    public class Return : AST {
        public Expression Expression;

        public override void Parse() {
            if (Scanner.IsEOL()) return;
            Expression = Expression.ParseExpression(this);
        }

        public override void Print() {
            base.Print();
            Expression?.Print();
        }

        public override void Save(TextWriter writer, Builder builder) {
            var func = FindParent<Function>();
            if (func.Type != null && func is not Constructor) {
                writer.Write("__RETURN__ = ");
                Expression?.Save(writer, builder);
                writer.WriteLine(";");
            }
            var block = Parent as Block;
            if (block.Defers.Count == 0) {
                writer.Write("goto __DONE__");
            } else {
                writer.Write("goto __DEFER__");
                writer.Write(block.Defers[0].ID);
            }
            writer.WriteLine(";");
        }
    }
}