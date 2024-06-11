namespace Run {
    public class Extension : Block {
        public Class Type;
        public override void Parse() {
            if (GetName(out Token) == false) return;

            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            base.Parse();
        }
    }
}