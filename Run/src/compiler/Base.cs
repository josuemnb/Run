namespace Run {
    internal class Base : ValueType {
        public Class Owner;
        public override void Parse() {
            Token = new Token { Value = "this" };
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
            if(cls.IsBased==false) {
                Program.AddError(Scanner.Current, Error.ClassDoesntHasBaseClass);
                Scanner.SkipLine();
                return;
            }
        }
    }
}