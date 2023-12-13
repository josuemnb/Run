using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace Run {
    public class ExpressionV2 : ValueType {
        public AST Result;
        public bool HasError { get; private set; }
        public override void Parse() {
            Scanner.Scan();
            Result = EvalExpression(this);
        }

        public override void Save(TextWriter writer, Builder builder) => Save(Result, writer, builder);
        void Save(AST ast, TextWriter writer, Builder builder) {
            switch (ast) {
                case Literal lit: writer.Write(lit.Token.Value); break;
                default: ast.Save(writer, builder); break;
            }
        }

        static ValueType Eval(ValueType parent, Func<ValueType, ValueType> func, params TokenType[] types) {
            var result = func(parent);
            if (result != null) {
                while (types.Contains(parent.Scanner.Current.Type)) {
                    var op = parent.Scanner.Current;
                    parent.Scanner.Scan();
                    var right = func(parent);
                    result = new Binary() { Token = op, Left = result, Right = right };
                }
            }
            return result;
        }

        static ValueType EvalExpression(ValueType parent) {
            var result = EvalAssign(parent);
            if (parent.Scanner.Current.Type == TokenType.TERNARY) {
                var ternary = new Ternary(result);
                ternary.SetParent(parent);
                ternary.Parse();
                return ternary;
            }
            return result;
        }

        static ValueType EvalAssign(ValueType parent) {
            return Eval(parent, EvalLogicalOr, TokenType.ASSIGN);
        }

        static ValueType EvalLogicalOr(ValueType parent) {
            return Eval(parent, EvalLogicalAnd, TokenType.OR);
        }
        static ValueType EvalLogicalAnd(ValueType parent) {
            return Eval(parent, EvalComparation, TokenType.AND);
        }

        static ValueType EvalComparation(ValueType parent) {
            return Eval(parent, EvalAddSub, TokenType.LOWER, TokenType.LOWER_OR_EQUAL, TokenType.EQUAL, TokenType.DIFFERENT, TokenType.GREATHER, TokenType.GREAT_OR_EQUAL);
        }
        static ValueType EvalAddSub(ValueType parent) {
            return Eval(parent, EvalMulDiv, TokenType.MINUS, TokenType.PLUS, TokenType.PLUS_ASSIGN, TokenType.MINUS_ASSIGN);
        }

        static ValueType EvalMulDiv(ValueType parent) {
            return Eval(parent, EvalUnary, TokenType.MULTIPLY, TokenType.DIVIDE, TokenType.MOD, TokenType.MULTIPLY_ASSIGN, TokenType.DIVIDE_ASSIGN);
        }

        static ValueType EvalUnary(ValueType parent) {
            Token unary = null;
            if (parent.Scanner.Current.Type == TokenType.PLUS || parent.Scanner.Current.Type == TokenType.MINUS || parent.Scanner.Current.Type == TokenType.INCREMENT || parent.Scanner.Current.Type == TokenType.DECREMENT) {
                unary = parent.Scanner.Current;
                parent.Scanner.Scan();
            }
            var ret = EvalPrimary(parent);
            if (unary != null) {
                return new Unary() { Token = unary, Right = ret };
            }
            return ParseOthers(parent, ret);
        }

        private static ValueType ParseOthers(ValueType parent, ValueType ret) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.INCREMENT:
                case TokenType.DECREMENT:
                    var unary = parent.Scanner.Current;
                    parent.Scanner.Scan();
                    return new Unary() { Token = unary, Right = ret };
                case TokenType.AS:
                    return ParseAs(parent, ret);
                case TokenType.DOT:
                    while (parent.Scanner.Current.Type == TokenType.DOT) {
                        var ma = new MemberAccess();
                        parent.Scanner.Scan();
                        ma.SetParent(parent);
                        ret.SetParent(ma);
                        ma.This = ret;
                        ma.Member = EvalExpression(parent);
                        ma.Member.SetParent(ma);
                        ret = ma;
                    }
                    break;
                case TokenType.OPEN_ARRAY:
                    while (parent.Scanner.Current.Type == TokenType.OPEN_ARRAY) {
                        var op = parent.Scanner.Current;
                        parent.Scanner.Scan();
                        var right = EvalExpression(parent);
                        if (parent.Scanner.Current.Type != TokenType.CLOSE_ARRAY) {
                            parent.Program.AddError(parent.Scanner.Current, Error.ExpectingEndOfArray);
                            return ret;
                        }
                        parent.Scanner.Scan();
                        ret = new Indexer() { Token = op, Left = ret, Right = right };
                    }
                    break;
            }
            return ret;
        }

        static ValueType ParseAs(ValueType parent, ValueType left) {
            if (parent.Scanner.Test().Type != TokenType.NAME) {
                parent.Program.AddError(Error.ExpectingName, parent);
                return left;
            }
            var a = new As();
            a.SetParent(parent);
            a.Left = left;
            //parent.Scanner.Scan();
            //a.Right = ParseExpression(a, PrecedenceLevel.Assignment);
            a.Declared = new DeclaredType();
            a.Declared.SetParent(a);
            a.Declared.Parse();
            parent.Scanner.Scan();
            return a;
        }

        static ValueType EvalPrimary(ValueType parent) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.NAME:
                    return EvalName(parent);
                case TokenType.QUOTE:
                case TokenType.NUMBER:
                case TokenType.REAL:
                case TokenType.CHAR:
                    var lit = new Literal() { Token = parent.Scanner.Current };
                    parent.Scanner.Scan();
                    return lit;
                case TokenType.OPEN_PARENTESES:
                    parent.Scanner.Scan();
                    return EvalParenteses(parent);
                default:
                    return parent;
            }
        }

        static ValueType EvalName(ValueType parent) {
            var token = parent.Scanner.Current;
            if (EvalKeyword(parent, token) is ValueType ret) {
                return ret;
            }
            if (parent.Scanner.Expect('(')) {
                var call = new CallerV2();
                call.SetParent(parent);
                call.Token = token;
                call.Parse();
                return call;
            }
            if (parent.Scanner.Expect('[')) {
                var array = new ArrayV2();
                array.SetParent(parent);
                array.Token = token;
                if (parent.Scanner.Expect(']') == false) {
                    array.Parse();
                }
                return array;
            }
            var id = new Identifier() { Token = token };
            id.SetParent(parent);
            parent.Scanner.Scan();
            return id;
        }

        static ValueType EvalKeyword(ValueType parent, Token token) {
            switch (token.Value) {
                case "base":
                    var b = new Base();
                    b.SetParent(parent);
                    b.Parse();
                    return b;
                case "delete":
                    var del = new Delete();
                    del.SetParent(parent);
                    del.Parse();
                    return del;
                case "ref":
                    var rf = new Ref();
                    rf.SetParent(parent);
                    rf.Parse();
                    return rf;
                case "sizeof":
                    var sz = new SizeOf();
                    sz.SetParent(parent);
                    sz.Parse();
                    return sz;
                case "cast":
                    var c = new Cast();
                    c.SetParent(parent);
                    c.Parse();
                    return c;
                case "true":
                case "false":
                    token.Type = TokenType.BOOL;
                    var tr = new Literal() { Token = token, };
                    tr.SetParent(parent);
                    parent.Scanner.Scan();
                    return tr;
                case "scope":
                    var s = new Scope();
                    s.SetParent(parent);
                    s.Parse();
                    return s;
                case "new":
                    var n = new New();
                    n.Token = token;
                    n.SetParent(parent);
                    n.Parse();
                    return n;
                case "this":
                    var cls = parent.FindParent<Class>();
                    if (cls == null) {
                        parent.Program.AddError(token, Error.UnknownName);
                        return null;
                    }
                    var t = new This();
                    t.Token = token;
                    t.Type = cls;
                    t.SetParent(parent);
                    parent.Scanner.Scan();
                    return t;
                case "null":
                    var nu = new Null {
                        Token = token,
                    };
                    nu.SetParent(parent);
                    parent.Scanner.Scan();
                    return nu;
                case "typeof":
                    var ty = new TypeOf();
                    ty.SetParent(parent);
                    ty.Parse();
                    return ty;
            }
            return null;
        }

        //public void Parse() {
        //    Scanner.Scan();
        //    var result = ParseExpression(this, PrecedenceLevel.Assignment);
        //    if (Scanner.Current.Type != TokenType.EOF) {
        //        //Module.AddError(scanner, "Expecting EOF");
        //    }
        //    //return result;
        //}

        //ValueType ParseExpression(AST parent, PrecedenceLevel precedence) {
        //    var result = EvalAssign(parent);
        //    while (precedence < Scanner.Current.Precedence) {
        //        var op = Scanner.Current;
        //        Scanner.Scan();
        //        var right = ParseExpression(parent, op.Precedence);
        //        result = new Binary() { Token = op, Left = result, Right = right };
        //    }
        //    return result;
        //}

        static ValueType EvalParenteses(ValueType parent) {
            var ret = EvalExpression(parent);
            if (parent.Scanner.Current.Type != TokenType.CLOSE_PARENTESES) {
                parent.Program.AddError(parent.Scanner.Current, Error.ExpectingCloseParenteses);
                return null;
            }
            var parenteses = new Parenteses();
            parenteses.SetParent(parent);
            parenteses.Scanner.Scan();
            parenteses.Expression = ret;
            return parenteses;
        }
    }

    public class ArrayV2 : CallerV2 {
        public ArrayV2() { End = TokenType.CLOSE_ARRAY; }
    }

    public class CallerV2 : ValueType {
        public List<AST> Values = new(0);
        public ValueType From;
        public Function Function;
        protected TokenType End = TokenType.CLOSE_PARENTESES;

        public override void Parse() {
            while (true) {
                var p = new ExpressionV2();
                p.SetParent(this);
                p.Parse();
                if (p == null) {
                    break;
                }
                p.Level = Level + 2;
                Values.Add(p);
                if (Scanner.Current.Type == End) {
                    Scanner.Scan();
                    break;
                }
                if (Scanner.Current.Type != TokenType.COMMA) {
                    Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                    break;
                }
                Scanner.Scan();
            }
        }
    }
}