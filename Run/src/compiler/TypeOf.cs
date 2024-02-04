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

        public override void Save(TextWriter writer, Builder builder) {
            if (Type != null && Type.ID >= 0 && Type.ID < Class.CounterID) {
                writer.Write("__TypesMap__[");
                writer.Write(Type.ID);
                writer.Write("]");
                return;
            }
            Program.AddError(Token, Error.InvalidExpression);
        }
    }
}