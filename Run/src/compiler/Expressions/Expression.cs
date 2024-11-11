using System;
using System.IO;

namespace Run {
    public enum PrecedenceLevel {
        None,
        Assignment, // =
        Ternary,    // ? :
        Or,         // or
        And,        // and
        BitwiseOr,  // |
        BitwiseAnd, // &
        Equality,   // == !=
        Comparison, // < > <= >=
        Shift,      // << >>
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
                case TokenType.SHIFT_LEFT:
                case TokenType.SHIFT_RIGHT: return (int)PrecedenceLevel.Shift;
                case TokenType.BITWISE_AND:
                    return (int)PrecedenceLevel.BitwiseAnd;
                case TokenType.BITWISE_OR:
                    return (int)PrecedenceLevel.BitwiseOr;
                case TokenType.ASSIGN:
                case TokenType.PLUS_ASSIGN:
                case TokenType.MINUS_ASSIGN:
                case TokenType.MULTIPLY_ASSIGN:
                case TokenType.DIVIDE_ASSIGN:
                case TokenType.AND_ASSIGN:
                case TokenType.OR_ASSIGN:
                case TokenType.IN:
                case TokenType.RANGE:
                case TokenType.IS:
                case TokenType.AS: return (int)PrecedenceLevel.Assignment;
            }
            return (int)PrecedenceLevel.None;
        }
    }

    public class Expression : ValueType {

        public bool HasError { get; private set; }

        public override void Print() {
            ExpressionHelper.Print(this);
        }

        public Expression Clone() {
            return (Expression)MemberwiseClone();
        }

        public void ParseGeneric(Token token) {
            var cls = FindParent<Class>();
            if (cls == null || cls?.HasGenerics == false) return;

            if (cls.Generics.Find(g => g.Token.Value == Token.Value) is Generic gen) {
                var func = FindParent<Function>();
                if (Generic != null) {
                    Program.AddError(token, Error.InvalidExpression);
                    Scanner.SkipLine();
                    return;
                }
                func.HasGeneric = true;
                Generic = gen;
            }
        }
    }

    internal class AssignExpression : BinaryExpression {

        public AssignExpression(AST parent, Expression left) : base(parent, left) {
            Right = ExpressionHelper.Parse(this);
        }
    }

    internal class RangeExpression : BinaryExpression {
        public RangeExpression(AST parent) : base(parent) {
            Right = ExpressionHelper.Parse(this);
        }
        public RangeExpression(AST parent, Expression left) : base(parent, left) {
            Right = ExpressionHelper.Parse(this);
        }
    }

    internal class Iterator : ContentExpression {
        public Var Var;
        public Iterator(AST parent) {
            SetParent(parent);
        }
        public Iterator(AST parent, Expression id) : this(parent) {
            if (id is not IdentifierExpression) {
                parent.Program.AddError(Scanner.Current, Error.InvalidExpression);
                return;
            }
            if (parent is not For) {
                parent.Program.AddError(Scanner.Current, Error.OnlyInsideForDeclaration);
                return;
            }
            Var = new Var() {
                Token = id.Token,
                Real = "_" + id.Token.Value,
                Scanner = parent.Scanner,
                Parent = this,
            };
            Var.SetParent(this);
            Content = ExpressionHelper.Parse(this);
        }
    }

    public class ContentExpression : Expression {
        public Expression Content;

        public override void Parse() {
            Content = ExpressionHelper.Parse(this);
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
            Content = ExpressionHelper.Parse(this);
            if (Scanner.Expect(')') == false) {
                //throw new Exception("Expected )");
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
            }
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
            True = ExpressionHelper.Parse(parent);
            if (Scanner.Expect(':') == false) {
                throw new Exception("Expected :");
            }
            False = ExpressionHelper.Parse(parent);
        }
    }

    internal class IndexerExpression : BinaryExpression {
        public IndexerExpression(AST parent, Expression left) : base(parent, left) {
            Right = ExpressionHelper.Parse(parent);
            if (Scanner.Expect(']') == false) {
                //throw new Exception("Expected ]");
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            }
        }
    }

    public class ArrayCreationExpression : ContentExpression {
        public ArrayCreationExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            Content = ExpressionHelper.Parse(this);
            if (Scanner.Expect(']') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            }
        }
    }

    public class ConstructorExpression(AST parent, bool parse = true) : CallExpression(parent, parse) {
    }

    public class DotExpression : BinaryExpression {
        public DotExpression(AST parent) : base(parent) { }
        public DotExpression(AST parent, Expression left, Expression expression) : base(parent, left, expression) {
        }
        public DotExpression(AST parent, Expression left) : base(parent, left) {
            Right = ExpressionHelper.Parse(this, (int)PrecedenceLevel.Call);
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

        public override string ToString() => Left.ToString() + " . " + Right.ToString();
    }

    public class TypeExpression : Expression {
        public TypeExpression(AST parent) {
            SetParent(parent);
            Token = Scanner.Current;
            Real = "_" + Token.Value;
            ParseGeneric(Token);
        }
    }

    internal class UnaryExpression : ContentExpression {
        public bool AtRight;
        public UnaryExpression(AST parent, bool atRight = false) {
            AtRight = atRight;
            SetParent(parent);
            Token = Scanner.Current;
            Content = ExpressionHelper.Parse(parent, (int)PrecedenceLevel.Unary);
        }
        public UnaryExpression(AST parent, Expression left, bool atRight = false) {
            AtRight = atRight;
            SetParent(parent);
            Token = Scanner.Current;
            Real = "_" + Token.Value;
            Content = left;
            Content.SetParent(this);
        }

    }


    internal class LiteralExpression : Expression {
        public LiteralExpression(AST parent, TokenType? type = null) {
            SetParent(parent);
            Token = Scanner.Current;
            if (type != null) {
                Token.Type = type.Value;
            }
        }
    }
}
