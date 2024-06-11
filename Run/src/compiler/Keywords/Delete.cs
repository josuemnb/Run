using System.IO;

namespace Run {
    public class Delete : ValueType {
        public Block Block;
        public override void Parse() {
            Block = new Block();
            Block.SetParent(this);
            bool parenteses = Scanner.Expect('(');
        again:
            if (GetName(out Token name) == false) {
                Scanner.SkipLine();
                return;
            }
            Block.Add<IdentifierExpression>().Token = name;
            if (Scanner.Expect(',')) {
                goto again;
            }
            if (parenteses) {
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
                }
            } else if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            }
        }
    }
}