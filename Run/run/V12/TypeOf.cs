using System.IO;

namespace Run.V12 {
    public class TypeOf : ValueType {
        public Expression Expression;

        public override void Parse() {
            if (Scanner.Expect('(') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingOpenParenteses);
                return;
            }
            Expression = new Expression();
            Expression.SetParent(this);
            Expression.Parse();
            if (Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Expression != null && Expression.Type != null && Expression.Type.ID >= 0 && Expression.Type.ID < Class.CounterID) {
                writer.Write("__TypesMap__[");
                writer.Write(Expression.Type.ID);
                writer.Write("]");
                return;
            }
            Program.AddError(Token, Error.InvalidExpression);
        }
    }
}