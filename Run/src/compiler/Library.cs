namespace Run {
    public class Library : AST {
        public override void Parse() {
            if (Scanner.Test().Type != TokenType.QUOTE) {
                Program.AddError(Scanner.Current, Error.ExpectingQuote);
                Scanner.SkipLine();
            }
            Token = Scanner.Scan();
        }
    }
}