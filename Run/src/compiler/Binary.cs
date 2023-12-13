using System.IO;

namespace Run {
    public class Binary : ValueType {
        public AST Left;
        public AST Right;
        public int End;
        public override void Print() {
            base.Print();
            Left.Level = Level + 1;
            Right.Level = Level + 1;
            Left.Print();
            Right.Print();
        }
        public override void Save(TextWriter writer, Builder builder) {
            Left.Save(writer, builder);
            writer.Write(Token.Value);
            Right.Save(writer, builder);
        }
    }

    public class Comparation : Binary { }

    public class Assign : Binary { }

    public class As : ValueType {
        public AST Left;
        public DeclaredType Declared;
        public override void Save(TextWriter writer, Builder builder) {
            //var number = (Type.IsNumber && Left is ValueType vt && vt.Type != null && vt.Type.IsPrimitive == false);
            //if (number) writer.Write("*");
            writer.Write("(");
            writer.Write(Type.Real);
            //if (number) {
            //    //writer.Write("*");
            //} else {
            if (Type.IsPrimitive == false) {
                writer.Write("*");
            }
            //if (Right is Array) {
            //    writer.Write("*");
            //}
            //}
            writer.Write(")");
            Left.Save(writer, builder);
        }
    }
}