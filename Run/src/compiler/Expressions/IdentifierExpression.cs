using System.IO;

namespace Run {
    internal class IdentifierExpression : Expression {
        public AST From;
        public bool Virtual;

        public IdentifierExpression() {

        }
        public IdentifierExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            Real = "_" + Token.Value;
        }

        public override string ToString() => base.ToString() + " \"" + Token.Value + "\"";
    }

}