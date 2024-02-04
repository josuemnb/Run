using System.IO;

namespace Run {
    public class ValueType : AST {
        public Class Type;
        public bool IsNull;
    }

    public class Scope : ValueType {
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                Scanner.SkipLine();
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("SCOPE(");
            writer.Write(Token.Value);
            writer.Write(")");
        }
    }

    public class PropertySetter(AST parent) : CallExpression(parent) {
        public int Back;
        public AST This;

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Real);
            writer.Write('(');
            This?.Save(writer, builder);
            writer.Write(')');
        }
    }

    public class Ref : ContentExpression {

        public Ref(AST parent) {
            SetParent(parent);
            Parse();
        }
        public override void Parse() {
            var parenteses = Scanner.Expect('(');
            base.Parse();
            if (parenteses && Scanner.Expect(')') == false) {
                Program.AddError(Token, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Type.IsPrimitive) {
                writer.Write("&(");
            } else
            if (Type.IsNumber) {
                writer.Write("&(");
            } else {
                writer.Write("*(");
            }
            Content.Save(writer, builder);
            writer.Write(')');
        }
    }
    public class SizeOf : ContentExpression {
        public SizeOf(AST parent) {
            SetParent(parent);
            Parse();
        }
        public override void Parse() {
            var parenteses = Scanner.Expect('(');
            base.Parse();
            if (parenteses && Scanner.Expect(')') == false) {
                Program.AddError(Token, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Content is IdentifierExpression id) {
                if (id.From is Var p && p.Arrays != null) {
                    writer.Write("SIZEOF(");
                    writer.Write(id.Token.Value);
                    writer.Write(")");
                    return;
                } else if (id.Type != null) {
                    writer.Write("sizeof(");
                    writer.Write(id.Type.Real);
                    writer.Write(')');
                    return;
                }
            } else if (Content is ThisExpression) {
                writer.Write("SIZEOF(this)");
                return;
            }
            writer.Write("sizeof(");
            Content.Save(writer, builder);
            writer.Write(')');
        }
    }
}
