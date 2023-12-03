using System.Collections.Generic;
using System.IO;

namespace Run.V12 {
    public class New : ValueType {
        public Expression Expression;
        //public DeclaredType Declared;
        public override void Parse() {
            Expression = new Expression();
            Expression.SetParent(this);
            Expression.Parse();
            //Declared = new DeclaredType();
            //Declared.SetParent(this);
            //Declared.Parse();
        }

        public override void Print() {
            base.Print();
            Expression?.Print();
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Expression == null) {
                return;
            }
            if (Expression.Result is Array arr) {
                writer.Write("NEW(");
                writer.Write(arr.Type.Real);
                arr.SaveValues(writer, builder, true);
                writer.Write(",");
                writer.Write(arr.Type.ID);
            } else if (Expression.Result is Caller call) {
                if (call.Type.IsNative == false) {
                    writer.Write(call.Real);
                    writer.Write("(");
                    writer.Write(Type.Real);
                    writer.Write("_initializer(");
                }
                writer.Write("NEW(");
                writer.Write(Type.Real);
                writer.Write(",1,");
                writer.Write(Type.ID);
                writer.Write(")");
                if (call.Type.IsNative) {
                    return;
                }
                writer.Write(')');
                if (call.Values.Count > 0) {
                    foreach (var value in call.Values) {
                        writer.Write(',');
                        value.Save(writer, builder);
                    }
                }
            }
            writer.Write(')');
        }
    }
}