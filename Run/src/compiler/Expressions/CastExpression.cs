using System.IO;

namespace Run {
    internal class AsExpression : BinaryExpression {
        public bool IsArray;
        public AsExpression(AST parent, Expression left) : base(parent, left) {
            if (Scanner.Test().Type != TokenType.NAME) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                Scanner.SkipLine();
                return;
            }
            Scanner.Scan();
            Right = new TypeExpression(this);
            if (Scanner.Expect('[')) {
                if (Scanner.Expect(']') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
                    Scanner.SkipLine();
                    return;
                }
                IsArray = true;
            }
        }
    }
    internal class IsExpression : AsExpression {
        public IsExpression(AST parent, Expression left) : base(parent, left) { }
    }
}