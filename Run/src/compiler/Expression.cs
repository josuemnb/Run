using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Run {
    public enum PrecedenceLevel {
        None,
        Assignment, // =
        Or,         // or
        And,        // and
        Ternary,    // ? :
        Equality,   // == !=
        Comparison, // < > <= >=
        Term,       // + -
        Factor,     // * /
        Unary,      // ! -
        Call,       // . ()
        Primary,
        Highest,
    }
    public static class PrecedenceExtensions {
        public static int Precedence(this TokenType kind) {
            switch (kind) {
                case TokenType.OR: return (int)PrecedenceLevel.Or;
                case TokenType.AND: return (int)PrecedenceLevel.And;
                case TokenType.EQUAL:
                case TokenType.NOT_EQUAL: return (int)PrecedenceLevel.Equality;
                case TokenType.LOWER:
                case TokenType.LOWER_OR_EQUAL:
                case TokenType.GREATHER:
                case TokenType.GREAT_OR_EQUAL: return (int)PrecedenceLevel.Comparison;
                case TokenType.PLUS:
                case TokenType.DIFFERENT:
                case TokenType.MINUS: return (int)PrecedenceLevel.Term;
                case TokenType.TERNARY: return (int)PrecedenceLevel.Ternary;
                case TokenType.MULTIPLY:
                case TokenType.MOD:
                case TokenType.DIVIDE: return (int)PrecedenceLevel.Factor;
                case TokenType.INCREMENT:
                case TokenType.DECREMENT:
                case TokenType.NOT: return (int)PrecedenceLevel.Unary;
                case TokenType.DOT:
                case TokenType.OPEN_ARRAY:
                case TokenType.OPEN_PARENTESES: return (int)PrecedenceLevel.Call;
                case TokenType.ASSIGN:
                case TokenType.PLUS_ASSIGN:
                case TokenType.MINUS_ASSIGN:
                case TokenType.MULTIPLY_ASSIGN:
                case TokenType.DIVIDE_ASSIGN:
                case TokenType.AND_ASSIGN:
                case TokenType.OR_ASSIGN:
                case TokenType.IN:
                case TokenType.RANGE:
                case TokenType.AS: return (int)PrecedenceLevel.Assignment;
            }
            return (int)PrecedenceLevel.None;
        }
    }

    public class Expression : ValueType {
        public static T FindParent<T>(Expression expression) where T : Expression {
            if (expression is T t) return t;
            return expression.Parent is Expression exp ? FindParent<T>(exp) : null;
        }

        public static AST FindAssignment(Expression expression) {
            if (expression is AssignExpression) return expression;
            if (expression.Parent is Var v) return v;
            return expression.Parent is Expression exp ? FindAssignment(exp) : null;
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
            if (expression is ContentExpression content) {
                foreach (var item in FindChildren<T>(content.Content)) {
                    yield return item;
                }
            }
        }
        public bool HasError { get; private set; }

        public override void Print() {
            PrintAST(this);
        }

        static void PrintAST(AST ast) {
            if (ast == null) return;
            ast.Level = ast.Parent.Level + 1;
            AST.Print(ast);
            switch (ast) {
                case ContentExpression p:
                    PrintAST(p.Content);
                    break;
                case BinaryExpression bin:
                    PrintAST(bin.Left);
                    PrintAST(bin.Right);
                    break;
                case CallExpression call:
                    PrintAST(call.Caller);
                    foreach (var child in call.Arguments) {
                        PrintAST(child);
                    }
                    break;
            }
        }

        public static Expression ParsePrefix(AST parent) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.NUMBER:
                case TokenType.QUOTE:
                case TokenType.BOOL:
                case TokenType.NULL:
                case TokenType.REAL:
                case TokenType.CHAR:
                    return new LiteralExpression(parent);
                case TokenType.NAME:
                    return ParseName(parent);
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

        public static Expression ParseSuffix(AST parent, Expression left) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.NUMBER:
                case TokenType.QUOTE:
                case TokenType.BOOL:
                case TokenType.NULL:
                case TokenType.REAL:
                case TokenType.CHAR:
                    return new LiteralExpression(parent);
                case TokenType.DOT:
                    //return new DotExpression(parent, left);
                    return ParseMemberAcess(parent, left);
                case TokenType.OPEN_PARENTESES:
                    return new CallExpression(parent) {
                        Token = left.Token,
                    };
                case TokenType.OPEN_ARRAY:
                    return new IndexerExpression(parent, left);
                case TokenType.IN:
                    return new Iterator(parent, left);
                case TokenType.AS:
                    return new CastExpression(parent, left);
                case TokenType.OPEN_BLOCK:
                    return new ObjectExpression(parent);
                case TokenType.NAME:
                    return ParseName(parent);
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
                    return ParseExpression(parent, parent.Scanner.Current.Type.Precedence());
            }
            return null;
        }

        private static Expression ParseMemberAcess(AST parent, Expression left) {
            while (parent.Scanner.Test().Type == TokenType.NAME) {
                parent.Scanner.Scan();
                left = new DotExpression(parent, left, new IdentifierExpression(parent));
                if (parent.Scanner.Expect('.') == false) {
                    break;
                }
            }
            return left;
        }

        private static Expression ParseName(AST parent) {
            switch (parent.Scanner.Current.Value) {
                case "new": return new NewExpression(parent);
                case "this": return new ThisExpression(parent);
                case "false":
                case "true": return new LiteralExpression(parent, TokenType.BOOL);
                case "null": return new LiteralExpression(parent, TokenType.NULL);
                case "sizeof": return new SizeOf(parent);
                case "ref": return new Ref(parent);
                case "typeof": return new TypeOf(parent);
                case "base": return new Base(parent);
                default: return new IdentifierExpression(parent);
            }
        }

        public static Expression ParseExpression(AST parent, int precedence = 1) {
            parent.Scanner.Scan();
            var left = ParsePrefix(parent);

            if (left == null) {
                parent.Program.AddError(parent.Scanner.Current, Error.InvalidExpression);
                return null;
            }

            while (precedence <= parent.Scanner.Test().Type.Precedence()) {
                var token = parent.Scanner.Scan();
                var right = ParseSuffix(parent, left);

                if (right == null) break;

                switch (token.Type) {
                    case TokenType.DOT:
                    case TokenType.TERNARY:
                    case TokenType.AS:
                    case TokenType.IN:
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

    internal class AssignExpression : BinaryExpression {

        public AssignExpression(AST parent, Expression left) : base(parent, left) {
            Right = ParseExpression(this);
        }
    }

    internal class RangeExpression : BinaryExpression {
        public RangeExpression(AST parent) : base(parent) {
            Right = ParseExpression(parent);
        }
        public RangeExpression(AST parent, Expression left) : base(parent, left) {
            Right = ParseExpression(parent);
        }
    }

    internal class Iterator : BinaryExpression {
        public Iterator(AST parent) : base(parent) {
        }
        public Iterator(AST parent, Expression id) : base(parent, id) {
            Right = ParseExpression(parent);
        }
    }

    public class ContentExpression : Expression {
        public Expression Content;

        public override void Parse() {
            Content = ParseExpression(this);
        }
    }

    internal class ThisExpression : Expression {

        public ThisExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
        }
    }

    internal class ParentesesExpression : ContentExpression {

        public ParentesesExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            Content = ParseExpression(this);
            if (Scanner.Expect(')') == false) {
                //throw new Exception("Expected )");
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write('(');
            Content.Save(writer, builder);
            writer.Write(')');
        }
    }

    internal class TernaryExpression : Expression {
        public Expression Condition;
        public Expression True;
        public Expression False;
        public TernaryExpression(AST parent, Expression condition) {
            SetParent(parent);
            Token = Scanner.Current;
            Condition = condition;
            Condition.SetParent(this);
            True = ParseExpression(parent);
            if (Scanner.Expect(':') == false) {
                throw new Exception("Expected :");
            }
            False = ParseExpression(parent);
        }

        public override void Save(TextWriter writer, Builder builder) {
            Condition.Save(writer, builder);
            writer.Write(" ? ");
            True.Save(writer, builder);
            writer.Write(" : ");
            False.Save(writer, builder);
        }
    }

    internal class NewExpression : ContentExpression {
        public string QualifiedName;
        public NewExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            while (char.IsLetter(Scanner.Peek())) {
                Scanner.Scan();
                if (Scanner.Current.Type != TokenType.NAME) {
                    Program.AddError(Scanner.Current, Error.ExpectingName);
                    Scanner.SkipLine();
                    return;
                }
                QualifiedName += Scanner.Current.Value;
                if (Scanner.Expect('.')) {
                    QualifiedName += '.';
                    continue;
                }
                break;
            }
            Token.Value = QualifiedName;
            if (Scanner.Expect('(')) {
                Content = new ConstructorExpression(this) {
                    Token = Token,
                };
            } else if (Scanner.Expect('[')) {
                Content = new ArrayCreationExpression(this) {
                    Token = Token,
                };
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Content is ArrayCreationExpression array) {
                writer.Write("(");
                writer.Write(array.Type.Real);
                if (array.Type.IsPrimitive == false) {
                    writer.Write("*");
                }
                writer.Write("*)malloc(sizeof(");
                writer.Write(array.Type.Real);
                if (array.Type.IsPrimitive == false) {
                    writer.Write("*");
                }
                writer.Write(") * ");
                array.Content.Save(writer, builder);
            } else if (Content is ConstructorExpression ctor) {
                if (ctor.Type.IsNative == false) {
                    writer.Write(ctor.Real);
                    writer.Write("(");
                    writer.Write(Type.Real);
                    writer.Write("_initializer(");
                }
                writer.Write("NEW(");
                writer.Write(Type.Real);
                writer.Write(",1,");
                writer.Write(Type.ID);
                writer.Write(")");
                if (ctor.Type.IsNative) {
                    return;
                }
                writer.Write(')');
                foreach (var value in ctor.Arguments) {
                    writer.Write(',');
                    value.Save(writer, builder);
                }
            } else {
                Debugger.Break();
            }
            writer.Write(')');
        }
    }

    internal class IndexerExpression : BinaryExpression {
        public IndexerExpression(AST parent, Expression left) : base(parent, left) {
            Right = ParseExpression(parent);
            if (Scanner.Expect(']') == false) {
                //throw new Exception("Expected ]");
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            Left.Save(writer, builder);
            writer.Write('[');
            Right.Save(writer, builder);
            writer.Write(']');
        }
    }

    public class ArrayCreationExpression : ContentExpression {
        public ArrayCreationExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            Content = ParseExpression(this);
            if (Scanner.Expect(']') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            }
        }
    }

    public class ConstructorExpression(AST parent) : CallExpression(parent, true) {
    }

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
                Arguments.Add(ParseExpression(parent));
                if (Scanner.Expect(',')) {
                    continue;
                }
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                }
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Function?.Real ?? Real ?? Token.Value);
            writer.Write('(');
            if (Caller != null && Function.Access != AccessType.STATIC) {
                Caller.Save(writer, builder);
                if (Arguments.Count > 0) writer.Write(", ");
            }
            for (int i = 0; i < Arguments.Count; i++) {
                Arguments[i].Save(writer, builder);
                if (i < Arguments.Count - 1) {
                    writer.Write(", ");
                }
            }
            writer.Write(')');
        }
    }

    public class DotExpression : BinaryExpression {
        public DotExpression(AST parent, Expression left, Expression expression) : base(parent, left, expression) {
        }
        public DotExpression(AST parent, Expression left) : base(parent, left) {
            Right = ParseExpression(this, (int)PrecedenceLevel.Call);
        }
        public override void Save(TextWriter writer, Builder builder) {
            Left.Save(writer, builder);
            writer.Write(Left.Type?.IsEnum ?? false ? "_" : "->");
            Right.Save(writer, builder);
        }
    }

    public class BinaryExpression : Expression {
        public Expression Left;
        public Expression Right;
        public BinaryExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
        }


        public BinaryExpression(AST parent, Expression left) : this(parent) {
            Left = left;
            Left?.SetParent(this);
        }

        public BinaryExpression(AST parent, Expression left, Expression expression) : this(parent, left) {
            Right = expression;
            Right.SetParent(this);
        }

        public override void Save(TextWriter writer, Builder builder) {
            Left.Save(writer, builder);
            writer.Write(Token.Value);
            Right.Save(writer, builder);
        }
        public override string ToString() => Left.ToString() + " . " + Right.ToString();
    }

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
                }, ParseExpression(this, 0));
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

    public class TypeExpression : Expression {
        public TypeExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
        }
    }

    internal class CastExpression : BinaryExpression {
        public bool IsArray;
        public CastExpression(AST parent, Expression left) : base(parent, left) {
            SetParent(parent);
            if (Scanner.Test().Type != TokenType.NAME) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                Scanner.SkipLine();
                return;
            }
            Scanner.Scan();
            Right = new TypeExpression(this);
            if (Scanner.Expect('[')) {
                if (Scanner.Expect(']') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
                    Scanner.SkipLine();
                    return;
                }
                IsArray = true;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Type == null) return;
            writer.Write("CAST(");
            writer.Write(Type.Real);
            if (Type.IsPrimitive == false) {
                writer.Write('*');
            }
            //for (int i = 0; i < Arrays; i++) writer.Write("*");
            writer.Write(", ");
            Left.Save(writer, builder);
            writer.Write(')');
        }

    }

    internal class UnaryExpression : ContentExpression {
        public bool AtRight;
        public UnaryExpression(AST parent, bool atRight = false) {
            AtRight = atRight;
            SetParent(parent);
            Token = Scanner.Current;
            Content = ParseExpression(parent, (int)PrecedenceLevel.Unary);
        }
        public UnaryExpression(AST parent, Expression left, bool atRight = false) {
            AtRight = atRight;
            SetParent(parent);
            Token = Scanner.Current;
            Content = left;
            Content.SetParent(this);
        }


        public override void Save(TextWriter writer, Builder builder) {
            if (AtRight == false) writer.Write(Token.Value);
            Content.Save(writer, builder);
            if (AtRight) writer.Write(Token.Value);
        }
    }

    internal class IdentifierExpression : Expression {
        public AST From;
        public bool Virtual;

        public IdentifierExpression() {

        }
        public IdentifierExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
        }
        public override void Save(TextWriter writer, Builder builder) {
            if (From != null) {
                if (From is Field f) {
                    if (f.Access != AccessType.STATIC && Parent is not DotExpression) {
                        writer.Write("this->");
                    }
                }
                writer.Write(From.Real ?? From.Token.Value);
                return;
            }
            writer.Write(Real ?? Token?.Value);
        }

        public override string ToString() => base.ToString() + " \"" + Token.Value + "\"";
    }

    internal class LiteralExpression : Expression {
        public LiteralExpression(AST parent, TokenType? type = null) {
            SetParent(parent);
            Token = Scanner.Current;
            if (type != null) {
                Token.Type = type.Value;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Real ?? Token?.Value);
        }
    }
}
