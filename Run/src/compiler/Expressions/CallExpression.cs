using System.Collections.Generic;
using System.IO;

namespace Run {
    public class CallExpression : Expression {
        public List<Expression> Arguments = new(0);
        public ValueType Caller;
        public Function Function;
        public CallExpression(AST parent, bool parse = true) {
            SetParent(parent);
            Token = Scanner.Current;

            if (parse == false) return;
            if (Scanner.Expect(')')) return;

            while (true) {
                Arguments.Add(ExpressionHelper.Expression(this));
                if (Scanner.Expect(',')) {
                    continue;
                }
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                }
                return;
            }
        }
    }
}