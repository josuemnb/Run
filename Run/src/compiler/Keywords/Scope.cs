namespace Run {
    public class Scope : ValueType {
        public Scope(AST parent) {
            Parent = parent;
        }

        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                Scanner.SkipLine();
            }
        }
    }
}