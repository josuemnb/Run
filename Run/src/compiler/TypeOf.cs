using System.IO;

namespace Run {
    public class TypeOf : ValueType {
        public ExpressionV2 Expression;

        public override void Parse() {
            if (Scanner.Expect('(') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingOpenParenteses);
                return;
            }
            Expression = new ExpressionV2();
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