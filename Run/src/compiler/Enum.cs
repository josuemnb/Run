using System.IO;

namespace Run {
    public class EnumMember : ValueType {
        public ExpressionV2 Expression;
        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Parent.Token.Value);
            writer.Write('_');
            writer.Write(Token.Value);
        }
    }
    public class Enum : Block {
        public Class Type;
        public int Usage;
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            Type = new Class {
                Token = Token,
                Scanner = Scanner,
            };
            while (true) {
                var token = Scanner.Scan();
                if (token == null) return;
                switch (token.Type) {
                    case TokenType.CLOSE_BLOCK: return;
                    case TokenType.EOL: break;
                    case TokenType.COMMENT: Scanner.SkipLine(); break;
                    case TokenType.NAME:
                        var bin = new EnumMember() {
                            Token = token,
                        };
                        Add(bin);
                        if (Scanner.Expect('=')) {
                            bin.Expression = new ExpressionV2();
                            bin.Expression.SetParent(bin);
                            bin.Expression.Parse();
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
