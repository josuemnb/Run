using System;
using System.IO;

namespace Run {
    public class EnumMember : ContentExpression {
    }
    public class Enum : Class {
        public Enum() {
            IsEnum = true;
        }
        public override void Parse() {
            if (GetName(out Token) == false) return;
            Real = "_" + Token.Value;
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
                        Program.LinesCompiled++;
                        break;
                    case TokenType.COMMENT:
                        Scanner.SkipLine();
                        Program.LinesCompiled++;
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
