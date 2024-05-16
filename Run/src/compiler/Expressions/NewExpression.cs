using System.Diagnostics;
using System.IO;

namespace Run {
    internal class NewExpression : ContentExpression {
        public string QualifiedName;
        public bool IsScoped;
        public NewExpression(AST parent, bool parse = true) {
            SetParent(parent);
            if (parse) Parse();
        }

        public override void Parse() {
            Token = Scanner.Current;
            while (char.IsLetter(Scanner.Peek())) {
                Scanner.Scan();
                if (Scanner.Current.Type != TokenType.NAME) {
                    Program.AddError(Scanner.Current, Error.ExpectingName);
                    Scanner.SkipLine();
                    return;
                }
                QualifiedName += Scanner.Current.Value;
                if (Scanner.Expect('.')) {
                    QualifiedName += '.';
                    continue;
                }
                break;
            }
            Token.Value = QualifiedName;
            if (Scanner.Expect('(')) {
                Content = new ConstructorExpression(this) {
                    Token = Token,
                };
            } else if (Scanner.Expect('[')) {
                Content = new ArrayCreationExpression(this) {
                    Token = Token,
                };
            }
        }
    }
}