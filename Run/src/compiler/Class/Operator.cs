using System.IO;

namespace Run {
    public class Operator : Function {
        public override void Parse() {
            (Parent as Class).HasOperators = true;
            SetAccess();
            bool err = false;
            if (Access != AccessType.INSTANCE) {
                Program.AddError(Scanner.Current, Error.ExpectedStaticAcess);
                err = true;
            }
            if (Modifier != AccessModifier.PUBLIC) {
                Program.AddError(Scanner.Current, Error.ExpectedPublicAcces);
                err = true;
            }
            if (err) {
                Scanner.SkipBlock();
                return;
            }
            Token = Scanner.Scan();
            if (Token.Family != TokenType.ARITMETIC && Token.Family != TokenType.LOGICAL) {
                Program.AddError(Token, Error.InvalidExpression);
                Scanner.SkipBlock();
                return;
            }
            if (Scanner.Expect('(') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingOpenParenteses);
                Scanner.SkipBlock();
                return;
            }
            if (Scanner.Expect(')')) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                Scanner.SkipBlock();
                return;
            }
            ParseParameters();
            if (Scanner.Expect(':')) {
                GetReturnType();
            }
            if (Scanner.Expect("=>")) {
                ParseArrow();
                return;
            }
            ParseBlock();
        }
    }
}