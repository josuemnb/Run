using System.Collections.Generic;

namespace Run {
    public static class ExpressionHelper {
        public static T FindParent<T>(Expression expression) where T : Expression {
            if (expression is T t) return t;
            return expression.Parent is Expression exp ? FindParent<T>(exp) : null;
        }

        public static AST FindAssignment(Expression expression) {
            if (expression is AssignExpression) return expression;
            if (expression.Parent is Var v) return v;
            return expression.Parent is Expression exp ? FindAssignment(exp) : null;
        }

        public static Expression Clone(Expression expression) {
            if (expression == null) return null;

            var clone = expression.Clone();
            clone.Token = expression.Token.Clone();
            switch (expression) {
                case BinaryExpression bin:
                    var cloneBin = clone as BinaryExpression;
                    cloneBin.Left = Clone(bin.Left);
                    cloneBin.Right = Clone(bin.Right);
                    break;
                case CallExpression call:
                    var cloneCall = clone as CallExpression;
                    cloneCall.Arguments = new List<Expression>();
                    foreach (var item in call.Arguments) {
                        cloneCall.Arguments.Add(Clone(item));
                    }
                    break;
                case ContentExpression content:
                    var cloneContent = clone as ContentExpression;
                    cloneContent.Content = Clone(content.Content);
                    break;
            }
            return clone;
        }

        public static IEnumerable<T> FindChildren<T>(Expression expression) {
            if (expression is T t) yield return t;
            if (expression is BinaryExpression bin) {
                foreach (var item in FindChildren<T>(bin.Left)) {
                    yield return item;
                }
                foreach (var item in FindChildren<T>(bin.Right)) {
                    yield return item;
                }
            }
            if (expression is CallExpression call && call.Arguments != null) {
                foreach (var item in call.Arguments) {
                    foreach (var child in FindChildren<T>(item)) {
                        yield return child;
                    }
                }
            }
            if (expression is ContentExpression content) {
                foreach (var item in FindChildren<T>(content.Content)) {
                    yield return item;
                }
            }
        }
        public static void Print(AST ast) {
            if (ast == null) return;
            ast.Level = ast.Parent.Level + 1;
            AST.Print(ast);
            switch (ast) {
                case ContentExpression p:
                    Print(p.Content);
                    break;
                case BinaryExpression bin:
                    Print(bin.Left);
                    Print(bin.Right);
                    break;
                case CallExpression call:
                    Print(call.Caller);
                    foreach (var child in call.Arguments) {
                        Print(child);
                    }
                    break;
            }
        }

        public static Expression Prefix(AST parent) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.NUMBER:
                case TokenType.QUOTE:
                case TokenType.BOOL:
                case TokenType.NULL:
                case TokenType.FLOAT:
                case TokenType.DOUBLE:
                case TokenType.CHAR:
                case TokenType.HEX:
                    return new LiteralExpression(parent);
                case TokenType.NAME:
                    return Name(parent);
                case TokenType.RANGE:
                    return new RangeExpression(parent);
                case TokenType.OPEN_PARENTESES:
                    return new ParentesesExpression(parent);
                case TokenType.IN:
                    return new Iterator(parent);
                case TokenType.NOT:
                case TokenType.MINUS:
                case TokenType.DOT:
                case TokenType.INCREMENT:
                case TokenType.DECREMENT:
                    return new UnaryExpression(parent);
            }
            return null;
        }

        public static Expression Suffix(AST parent, Expression left) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.NUMBER:
                case TokenType.QUOTE:
                case TokenType.BOOL:
                case TokenType.NULL:
                case TokenType.FLOAT:
                case TokenType.DOUBLE:
                case TokenType.CHAR:
                case TokenType.HEX:
                    return new LiteralExpression(parent);
                case TokenType.DOT:
                    //return new DotExpression(parent, left);
                    return MemberAcess(parent, left);
                case TokenType.OPEN_PARENTESES:
                    return new CallExpression(parent) {
                        Token = left.Token,
                    };
                case TokenType.OPEN_ARRAY:
                    return new IndexerExpression(parent, left);
                case TokenType.IN:
                    return new Iterator(parent, left);
                case TokenType.AS:
                    return new AsExpression(parent, left);
                case TokenType.IS:
                    return new IsExpression(parent, left);
                case TokenType.OPEN_BLOCK:
                    return new ObjectExpression(parent);
                case TokenType.NAME:
                    return Name(parent);
                case TokenType.RANGE:
                    return new RangeExpression(parent, left);
                case TokenType.ASSIGN:
                    return new AssignExpression(parent, left);
                case TokenType.TERNARY:
                    return new TernaryExpression(parent, left);
                case TokenType.INCREMENT:
                case TokenType.DECREMENT:
                    return new UnaryExpression(parent, left, true);
                case TokenType.MOD:
                case TokenType.PLUS_ASSIGN:
                case TokenType.MINUS_ASSIGN:
                case TokenType.MULTIPLY_ASSIGN:
                case TokenType.DIVIDE_ASSIGN:
                case TokenType.AND_ASSIGN:
                case TokenType.OR_ASSIGN:
                case TokenType.PLUS:
                case TokenType.MINUS:
                case TokenType.MULTIPLY:
                case TokenType.DIVIDE:
                case TokenType.OR:
                case TokenType.AND:
                case TokenType.EQUAL:
                case TokenType.DIFFERENT:
                case TokenType.NOT_EQUAL:
                case TokenType.LOWER:
                case TokenType.LOWER_OR_EQUAL:
                case TokenType.GREATHER:
                case TokenType.GREAT_OR_EQUAL:
                case TokenType.SHIFT_RIGHT:
                case TokenType.SHIFT_LEFT:
                case TokenType.BITWISE_AND:
                case TokenType.BITWISE_OR:
                    return Parse(parent, parent.Scanner.Current.Type.Precedence());
            }
            return null;
        }

        private static Expression MemberAcess(AST parent, Expression left) {
            while (parent.Scanner.Test().Type == TokenType.NAME) {
                parent.Scanner.Scan();
                var id = new IdentifierExpression(parent);
                left = new DotExpression(parent, left, id);
                if (parent.Scanner.Expect('.') == false) {
                    break;
                }
            }
            return left;
        }

        private static Expression Name(AST parent) {
            switch (parent.Scanner.Current.Value) {
                case "new": return new NewExpression(parent);
                case "this": return new ThisExpression(parent);
                case "false":
                case "true": return new LiteralExpression(parent, TokenType.BOOL);
                case "null": return new LiteralExpression(parent, TokenType.NULL);
                case "sizeof": return new SizeOf(parent);
                case "scope": return new NewExpression(parent) { IsScoped = true };
                case "ref": return new Ref(parent);
                case "valueof": return new ValueOf(parent);
                case "typeof": return new TypeOf(parent);
                case "base": return new Base(parent);
                default: return new IdentifierExpression(parent);
            }
        }

        public static Expression Parse(AST parent, int precedence = 1) {
            parent.Scanner.Scan();
            var left = Prefix(parent);

            if (left == null) {
                parent.Program.AddError(parent.Scanner.Current, Error.InvalidExpression);
                return null;
            }

            while (precedence <= parent.Scanner.Test().Type.Precedence()) {
                var token = parent.Scanner.Scan();
                var right = Suffix(parent, left);

                if (right == null) break;

                switch (token.Type) {
                    case TokenType.DOT:
                    case TokenType.TERNARY:
                    case TokenType.AS:
                    case TokenType.IN:
                    case TokenType.IS:
                    case TokenType.RANGE:
                    case TokenType.INCREMENT:
                    case TokenType.DECREMENT:
                    case TokenType.OPEN_ARRAY:
                    case TokenType.ASSIGN:
                        left = right;
                        break;
                    case TokenType.OPEN_PARENTESES:
                        // the tree needs to be manipulated so the call cames first in order to help the transpiling step
                        if (left is DotExpression dot) {
                            var call = right as CallExpression;
                            call.Caller = dot.Left;
                            call.Caller.Parent = call;
                        }
                        left = right;
                        break;
                    //case TokenType.OPEN_ARRAY when left is IdentifierExpression id:
                    //    left = right;
                    //    left.Token = id.Token;
                    //    break;
                    default:
                        left = new BinaryExpression(parent, left, right) {
                            Token = token,
                        };
                        break;
                }
            }

            return left;
        }
    }
}
