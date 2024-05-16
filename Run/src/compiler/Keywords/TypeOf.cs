using System.IO;

namespace Run {
    public class TypeOf : ContentExpression {

        public TypeOf(AST parent) {
            SetParent(parent);
            Parse();
        }

        public override void Parse() {
            if (Scanner.Expect('(') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingOpenParenteses);
                return;
            }
            base.Parse();
            if (Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
            }
        }
    }
}