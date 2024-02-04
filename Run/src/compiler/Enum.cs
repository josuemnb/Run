using System;
using System.IO;

namespace Run {
    public class EnumMember : ContentExpression {
        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Parent.Token.Value);
            writer.Write('_');
            writer.Write(Token.Value);
        }

        public void SaveDeclaration(TextWriter writer, Builder builder) {
            var parent = Parent as Enum;
            writer.Write(parent.Real);
            if (Type.IsPrimitive == false) {
                writer.Write('*');
            }
            writer.Write(' ');
            Save(writer, builder);
            writer.Write(" = ");
            if (Content != null) {
                if (Content is LiteralExpression literal && Type == builder.String) {
                    writer.Write(literal.Token.Value);
                } else {
                    Content.Save(writer, builder);
                }
            } else {
                writer.Write(parent.Children.IndexOf(this));
            }
            writer.WriteLine(";");
        }
    }
    public class Enum : Class {
        public Enum() {
            IsEnum = true;
        }
        public override void Parse() {
            if (GetName(out Token) == false) return;
            Real = Token.Value;
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            while (true) {
                var token = Scanner.Scan();
                if (token == null) return;
                switch (token.Type) {
                    case TokenType.CLOSE_BLOCK: return;
                    case TokenType.EOL:
                        Program.Lines++;
                        break;
                    case TokenType.COMMENT:
                        Scanner.SkipLine();
                        Program.Lines++;
                        break;
                    case TokenType.NAME:
                        var member = new EnumMember() {
                            Token = token,
                        };
                        Add(member);
                        if (Scanner.Expect('=')) {
                            member.Parse();
                        }
                        break;
                    default:
                        Program.AddError(token, Error.UnknownName);
                        return;
                }
            }
        }
    }
}
