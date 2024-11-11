using System.IO;

namespace Run {
    public class ValueType : AST {
        public Class Type;
        public Generic Generic;
        public bool IsNull;
    }

    public class PropertySetter(AST parent) : CallExpression(parent) {
        public int Back;
        public AST This;
    }
}
