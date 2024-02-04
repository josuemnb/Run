namespace Run {
    internal class Base : Expression {
        public Class Owner;

        public Base(AST parent) {
            SetParent(parent);
            Parse();
        }
        public override void Parse() {
            Token = Scanner.Current;
            var cls = FindParent<Class>();
            if (cls == null) {
                Program.AddError(Scanner.Current, Error.BaseMustBeInsideClass);
                Scanner.SkipLine();
                return;
            }
            Owner = cls;
            var function = FindParent<Function>();
            if (function == null) {
                Program.AddError(Scanner.Current, Error.BaseMustBeInsideClass);
                Scanner.SkipLine();
                return;
            }
            if (cls.IsBased == false) {
                Program.AddError(Scanner.Current, Error.ClassDoesntHasBaseClass);
                Scanner.SkipLine();
                return;
            }
            Scanner.Scan();
        }
    }
}