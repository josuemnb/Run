using System.IO;

namespace Run {
    public class ValueType : AST {
        public Class Type;
        public bool IsNull;
    }

    public class PropertySetter(AST parent) : CallExpression(parent) {
        public int Back;
        public AST This;
    }
}
