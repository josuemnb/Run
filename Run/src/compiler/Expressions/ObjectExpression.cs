using System.Collections.Generic;
using System;

namespace Run {
    internal class ObjectExpression : Expression {
        public List<BinaryExpression> Assignments = new(0);
        public ObjectExpression(AST parent) {
            SetParent(parent);

            if (Scanner.Expect('}')) return;

            while (true) {
                var token = Scanner.Scan();
                if (token.Type != TokenType.NAME) {
                    throw new Exception("Expected name");
                }
                if (Scanner.Scan().Type != TokenType.ASSIGN) {
                    throw new Exception("Expected =");
                }
                var bin = new BinaryExpression(this, new IdentifierExpression(this) {
                    Token = token,
                }, ExpressionHelper.Expression(this, 0));
                if (Scanner.Scan().Type == TokenType.COMMA) {
                    continue;
                }
                if (Scanner.Expect('}') == false) {
                    throw new Exception("Expected , or }");
                }
                Assignments.Add(bin);
            }
        }

    }

}