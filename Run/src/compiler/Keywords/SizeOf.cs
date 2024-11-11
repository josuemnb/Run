namespace Run {
    public class SizeOf : ContentExpression {
        public SizeOf(AST parent) {
            SetParent(parent);
            Parse();
        }
        public override void Parse() {
            var parenteses = Scanner.Expect('(');
            base.Parse();
            if (parenteses && Scanner.Expect(')') == false) {
                Program.AddError(Token, Error.ExpectingCloseParenteses);
            }
            if (FindParent<Class>() is Class cls && cls.HasGenerics) {
                switch (Content) {
                    case IdentifierExpression id:
                        if (cls.Generics.Find(g => g.Token.Value == id.Token.Value) is Generic gen) {
                            Generic = gen;
                            if (FindParent<Function>() is Function function) {
                                function.HasGeneric = true;
                            }
                            if (FindParent<Var>() is Var var) {
                                var.InitializerHasGeneric = true;
                            }
                        }
                        break;
                }
            }
        }
    }
}