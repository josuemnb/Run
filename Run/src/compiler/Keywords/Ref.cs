using System.IO;

namespace Run {
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
    }
}