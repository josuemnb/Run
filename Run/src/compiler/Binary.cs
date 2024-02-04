using System.Collections.Generic;
using System.IO;

namespace Run {
    //public class Binary : ValueType {
    //    public AST Left;
    //    public AST Right;
    //    public int End;
    //    public override void Print() {
    //        base.Print();
    //        Left.Level = Level + 1;
    //        Right.Level = Level + 1;
    //        Left.Print();
    //        Right.Print();
    //    }
    //    public override void Save(TextWriter writer, Builder builder) {
    //        Left.Save(writer, builder);
    //        writer.Write(Token.Value);
    //        Right.Save(writer, builder);
    //    }
    //}

    //public class MemberAccess : Binary {
    //    public override void Save(TextWriter writer, Builder builder) {
    //        switch (Right) {
    //            case Caller cv2:
    //                writer.Write(cv2.Real);
    //                writer.Write('(');
    //                if (cv2.Function?.Access == AccessType.STATIC) {
    //                    cv2.SaveValues(writer, builder, false);
    //                } else {
    //                    Left.Save(writer, builder);
    //                    cv2.SaveValues(writer, builder, true);
    //                }
    //                writer.Write(')');
    //                break;
    //            //case Array array:
    //            //    array.Save(writer, builder);
    //            //    break;
    //            //case MemberAccess access:
    //            //    access.Save(writer, builder);
    //            //    //This.Save(writer, builder);
    //            //    //writer.Write("->");
    //            //    //access.Save(writer, builder);
    //            //    break;
    //            //case Caller call:
    //            //    writer.Write(call.Real);
    //            //    writer.Write('(');
    //            //    if (call.Function.Access == AccessType.STATIC) {
    //            //        call.SaveValues(writer, builder, false);
    //            //    } else {
    //            //        if (Parent is MemberAccess) {
    //            //            var buffer = new StringWriter();
    //            //            WriteParentAcess(this, buffer, builder);
    //            //            writer.Write(buffer.ToString());
    //            //            if (Left is Identifier id) {
    //            //                id.From = null;
    //            //            }
    //            //        }
    //            //        Left.Save(writer, builder);
    //            //        call.SaveValues(writer, builder, true);
    //            //    }
    //            //    writer.Write(')');
    //            //    break;
    //            case Identifier id:
    //                if (id.From is EnumMember en) {
    //                    en.Save(writer, builder);
    //                    break;
    //                }
    //                if (Parent is MemberAccess) {
    //                    writer.Write(Left.Token.Value);
    //                } else {
    //                    Left.Save(writer, builder);
    //                }
    //                writer.Write("->");
    //                writer.Write(id.Token.Value);
    //                //id.Save(writer, builder);
    //                break;
    //            default:
    //                Left.Save(writer, builder);
    //                if (Right is not null) {
    //                    writer.Write("->");
    //                    Right.Save(writer, builder);
    //                }
    //                break;
    //        }
    //    }

    //    void WriteParentAcess(AST ast, TextWriter writer, Builder builder) {
    //        if (ast.Parent is MemberAccess pa) {
    //            WriteParentAcess(pa, writer, builder);
    //            pa.Left.Save(writer, builder);
    //            writer.Write("->");
    //        }
    //    }

    //    public override void Print() {
    //        base.Print();
    //        Left.Level = Level + 1;
    //        Right.Level = Level + 1;
    //        Left.Print();
    //        Right.Print();
    //    }

    //    public override string ToString() {
    //        return Left + "." + Right;
    //    }
    //}

    //public class Comparation : Binary { }

    //public class Assign : Binary { }

    //public class As : ValueType {
    //    public AST Left;
    //    public DeclaredType Declared;
    //    public override void Save(TextWriter writer, Builder builder) {
    //        //var number = (Type.IsNumber && Left is ValueType vt && vt.Type != null && vt.Type.IsPrimitive == false);
    //        //if (number) writer.Write("*");
    //        writer.Write("(");
    //        writer.Write(Type.Real);
    //        //if (number) {
    //        //    //writer.Write("*");
    //        //} else {
    //        if (Type.IsPrimitive == false) {
    //            writer.Write("*");
    //        }
    //        //if (Right is Array) {
    //        //    writer.Write("*");
    //        //}
    //        //}
    //        writer.Write(")");
    //        Left.Save(writer, builder);
    //    }
    //}

    //public class Qualifier : Binary {
    //}
}