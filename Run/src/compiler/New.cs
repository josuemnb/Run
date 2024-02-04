using System.IO;

namespace Run {
    //public class New : ValueType {
    //    //public ExpressionV2 Expression;
    //    public Caller Caller;
    //    public ValueType Calling;
    //    public bool IsArray;
    //    //example
    //    //new int[5]
    //    //new string("hello")
    //    //new nmspc.class()

    //    public override void Parse() {
    //    again:
    //        if (GetName(out Token) == false) {
    //            return;
    //        }
    //        if (Calling == null) {
    //            Calling = new Identifier {
    //                Token = Token,
    //            };
    //            Calling.SetParent(this);
    //        } else {
    //            Calling = new Qualifier {
    //                Left = Calling,
    //                Right = new Identifier {
    //                    Token = Token,
    //                },
    //            };
    //            Calling.SetParent(this);
    //            (Calling as Qualifier).Right.SetParent(Calling);
    //        }
    //        if (Scanner.Expect('.')) {
    //            goto again;
    //        }
    //        if (Scanner.Expect('(')) {
    //            Caller = new Caller {
    //                Token = Token,
    //                From = Calling,
    //            };
    //            Caller.SetParent(this);
    //            Caller.Parse();
    //            Scanner.Scan();
    //        } else if (Scanner.Expect('[')) {
    //            IsArray = true;
    //            Caller = new Array {
    //                Token = Token,
    //                From = Calling,
    //            };
    //            Caller.SetParent(this);
    //            Caller.Parse();
    //            Scanner.Scan();
    //        } else {
    //            Program.AddError(Scanner.Current, Error.ExpectingOpenParentesesOrBrackets);
    //            return;
    //        }
    //        //Expression = new ExpressionV2();
    //        //Expression.SetParent(this);
    //        //Expression.Parse();
    //    }

    //    public override void Print() {
    //        base.Print();
    //        //Expression?.Print();
    //        Caller?.Print();
    //    }

    //    public override void Save(TextWriter writer, Builder builder) {
    //        if (Caller is Array array) {
    //            //(T*)malloc(sizeof(T) * total)
    //            writer.Write("(");
    //            writer.Write(array.Type.Real);
    //            if (array.Type.IsPrimitive == false) {
    //                writer.Write("*");
    //            }
    //            writer.Write("*)malloc(sizeof(");
    //            writer.Write(array.Type.Real);
    //            if (array.Type.IsPrimitive == false) {
    //                writer.Write("*");
    //            }
    //            //writer.Write("NEW(");
    //            writer.Write(") * ");
    //            array.SaveValues(writer, builder, false);
    //            //writer.Write(",");
    //            //writer.Write(array.Type.ID);
    //        } else {
    //            if (Caller.Type.IsNative == false) {
    //                writer.Write(Caller.Real);
    //                writer.Write("(");
    //                writer.Write(Type.Real);
    //                writer.Write("_initializer(");
    //            }
    //            writer.Write("NEW(");
    //            writer.Write(Type.Real);
    //            writer.Write(",1,");
    //            writer.Write(Type.ID);
    //            writer.Write(")");
    //            if (Caller.Type.IsNative) {
    //                return;
    //            }
    //            writer.Write(')');
    //            if (Caller.Parameters.Count > 0) {
    //                foreach (var value in Caller.Parameters) {
    //                    writer.Write(',');
    //                    value.Save(writer, builder);
    //                }
    //            }
    //        }
    //        writer.Write(')');
    //    }
    //}
}