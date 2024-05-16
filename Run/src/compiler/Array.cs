using System.Collections.Generic;

namespace Run {
    public class Array : ValueType {
        public List<Expression> Arguments;
        public bool IsScoped = true;
        public bool TypeArray;
        public void ParseArguments() {
            Arguments = new(0);
            if (Scanner.Expect(']')) {
                return;
            }
        again:
            if (Scanner.Peek() != ']' && Scanner.Peek() != ',')
                Arguments.Add(ExpressionHelper.Expression(this));
            if (Scanner.Expect(',')) {
                goto again;
            }
            if (Scanner.Expect(']') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            }
        }
    }
}
