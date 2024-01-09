using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Run {
    public class Expression : ValueType {
        public AST Result;
        public bool FromNewKeyword;
        public bool HasError { get; private set; }

        public override string ToString() {
            return Result?.ToString();
        }

        public override void Print() {
            AST.Print(this);
            if (Result != null) {
                Result.Level = Level + 1;
                PrintAST(Result);
            }
        }

        void PrintAST(AST ast) {
            if (ast == null) return;
            AST.Print(ast);
            switch (ast) {
                case Block b:
                    foreach (var child in b.Children) {
                        child.Level = ast.Level + 1;
                        PrintAST(child);
                    }
                    break;
                case Expression exp:
                    PrintAST(exp.Result);
                    break;
                case MemberAccess ma:
                    ma.Left.Level = ast.Level + 1;
                    ma.Right.Level = ast.Level + 1;
                    PrintAST(ma.Left);
                    PrintAST(ma.Right);
                    break;
                case Binary bin:
                    bin.Left.Level = ast.Level + 1;
                    bin.Right.Level = ast.Level + 1;
                    PrintAST(bin.Left);
                    PrintAST(bin.Right);
                    break;
                case Unary un:
                    un.Right.Level = ast.Level + 1;
                    PrintAST(un.Right);
                    break;
                case Parenteses par:
                    par.Expression.Level = ast.Level + 1;
                    PrintAST(par.Expression);
                    break;
                case Caller call:
                    if (call.From != null) {
                        call.From.Level = ast.Level + 1;
                        PrintAST(call.From);
                    }
                    foreach (var child in call.Parameters) {
                        child.Level = ast.Level + 2;
                        PrintAST(child);
                    }
                    break;
                case Ternary ter:
                    ter.Condition.Level = ast.Level + 1;
                    ter.IsTrue.Level = ast.Level + 1;
                    ter.IsFalse.Level = ast.Level + 1;
                    PrintAST(ter.Condition);
                    PrintAST(ter.IsTrue);
                    PrintAST(ter.IsFalse);
                    break;
                case As a:
                    a.Level = ast.Level + 1;
                    a.Declared.Level = ast.Level + 1;
                    PrintAST(a.Left);
                    PrintAST(a.Declared);
                    break;
                case New n:
                    //n.Level = ast.Level + 1;
                    n.Caller.Level = ast.Level + 1;
                    n.Calling.Level = ast.Level + 1;
                    //PrintAST(n.Calling);
                    PrintAST(n.Caller);
                    break;
                case SizeOf sz:
                    sz.Expression.Level = ast.Level + 1;
                    PrintAST(sz.Expression);
                    break;
                case TypeOf ty:
                    ty.Expression.Level = ast.Level + 1;
                    PrintAST(ty.Expression);
                    break;
                case Delete del:
                    del.Block.Level = ast.Level + 1;
                    PrintAST(del.Block);
                    break;
                case Ref rf:
                    rf.Expression.Level = ast.Level + 1;
                    PrintAST(rf.Expression);
                    break;
            }
        }

        public override void Parse() {
            Scanner.Scan();
            Result = EvalExpression(this);
        }

        public override void Save(TextWriter writer, Builder builder) => Save(Result, writer, builder);
        void Save(AST ast, TextWriter writer, Builder builder) {
            switch (ast) {
                //case Literal lit: writer.Write(lit.Token.Value); break;
                //case Literal lit: lit.Save(writer, builder); break;
                default: ast.Save(writer, builder); break;
            }
        }

        static ValueType Eval<T>(ValueType parent, Func<ValueType, ValueType> func, params TokenType[] types) where T : Binary, new() {
            var result = func(parent);
            if (result != null) {
                while (types.Contains(parent.Scanner.Current.Type)) {
                    var op = parent.Scanner.Current;
                    parent.Scanner.Scan();
                    var right = func(parent);
                    if (right == result) {
                        return result;
                    }
                    result = new T() { Token = op, Left = result, Right = right };
                    result.SetParent(parent);
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
            return Eval<Assign>(parent, EvalLogicalOr, TokenType.ASSIGN);
        }

        static ValueType EvalLogicalOr(ValueType parent) {
            return Eval<Binary>(parent, EvalLogicalAnd, TokenType.OR);
        }
        static ValueType EvalLogicalAnd(ValueType parent) {
            return Eval<Binary>(parent, EvalComparation, TokenType.AND);
        }

        static ValueType EvalComparation(ValueType parent) {
            return Eval<Binary>(parent, EvalAddSub, TokenType.LOWER, TokenType.LOWER_OR_EQUAL, TokenType.EQUAL, TokenType.DIFFERENT, TokenType.GREATHER, TokenType.GREAT_OR_EQUAL);
        }
        static ValueType EvalAddSub(ValueType parent) {
            return Eval<Binary>(parent, EvalMulDiv, TokenType.MINUS, TokenType.PLUS, TokenType.PLUS_ASSIGN, TokenType.MINUS_ASSIGN);
        }

        static ValueType EvalMulDiv(ValueType parent) {
            return Eval<Binary>(parent, EvalUnary, TokenType.MULTIPLY, TokenType.DIVIDE, TokenType.MOD, TokenType.MULTIPLY_ASSIGN, TokenType.DIVIDE_ASSIGN);
        }

        static ValueType EvalUnary(ValueType parent) {
            Token unary = null;
            if (parent.Scanner.Current.Type == TokenType.MINUS || parent.Scanner.Current.Type == TokenType.INCREMENT || parent.Scanner.Current.Type == TokenType.DECREMENT) {
                unary = parent.Scanner.Current;
                parent.Scanner.Scan();
            }
            var ret = EvalOthers(parent);
            if (unary != null) {
                var un = new Unary() { Token = unary, Right = ret };
                un.SetParent(parent);
                return un;
            } else if (parent.Scanner.Current.Type == TokenType.INCREMENT || parent.Scanner.Current.Type == TokenType.DECREMENT) {
                unary = parent.Scanner.Current;
                parent.Scanner.Scan();
                var un = new Unary() { Token = unary, Right = ret, AtRight = true, };
                un.SetParent(parent);
                return un;
            }
            return ret;
            //return ParseOthers(parent, ret);
            //return EvalMemberAccess(ret);
        }

        static ValueType EvalOthers(ValueType value) {
            var result = EvalPrimary(value);
            return EvalExtras(result);
        }

        static ValueType EvalExtras(ValueType value) {
            while (true) {
                switch (value.Scanner.Current.Type) {
                    case TokenType.DOT:
                        value = EvalMemberAccess(value);
                        break;
                    case TokenType.AS:
                        value = EvalAs(value);
                        break;
                    case TokenType.OPEN_PARENTESES:
                        value = EvalInvocation(value, value.Token);
                        break;
                    case TokenType.OPEN_ARRAY:
                        value = EvalIndexer(value, value.Token);
                        break;
                    default:
                        return value;
                }
            }
        }

        static ValueType EvalMemberAccess(ValueType value) {
            var ma = new MemberAccess {
                Left = value,
                Token = value.Scanner.Current,
            };
            ma.SetParent(value.Parent);
            value.SetParent(ma);
            value.Scanner.Scan();
            var token = value.Scanner.Current;
            if (token.Type != TokenType.NAME) {
                value.Program.AddError(token, Error.ExpectingName);
                return value;
            }
            if (value.Scanner.Peek() == '(') {
                value.Scanner.Scan();
                ma.Right = EvalInvocation(value, token);
            } else if (value.Scanner.Peek() == '[') {
                value.Scanner.Scan();
                ma.Right = EvalIndexer(value, token);
            } else {
                ma.Right = EvalName(ma);
            }
            return ma;
        }

        static ValueType EvalParameterList<T>(ValueType value, Func<ValueType, ValueType> func, Token token, params TokenType[] types) where T : Callable, new() {
        again:
            value = func?.Invoke(value) ?? value;
            if (types.Contains(value.Scanner.Current.Type)) {
                var ret = new T();
                ret.Token = token;
                ret.From = value;
                ret.SetParent(value.Parent);
                ret.Parse();
                value = ret;
                if (func != null) {
                    goto again;
                }
            }
            return value;
        }

        static ValueType EvalIndexer(ValueType parent, Token token) {
            return EvalParameterList<Array>(parent, null, token, TokenType.OPEN_ARRAY);
        }

        static ValueType EvalInvocation(ValueType parent, Token token) {
            return EvalParameterList<Caller>(parent, null, token, TokenType.OPEN_PARENTESES);
        }

        static ValueType EvalAs(ValueType parent) {
            var token = parent.Scanner.Test();
            if (token.Type != TokenType.NAME) {
                parent.Program.AddError(Error.ExpectingName, parent);
                return parent;
            }
            var a = new As();
            a.Token = token;
            a.SetParent(parent.Parent);
            a.Left = parent;
            a.Declared = new DeclaredType();
            a.Declared.SetParent(a);
            a.Declared.Parse();
            parent.Scanner.Scan();
            return a;
        }

        private static ValueType ParseOthers(ValueType parent, ValueType value) {
            switch (parent.Scanner.Current.Type) {
                case TokenType.INCREMENT:
                case TokenType.DECREMENT:
                    var unary = parent.Scanner.Current;
                    parent.Scanner.Scan();
                    var un = new Unary() { Token = unary, Right = value };
                    un.SetParent(parent);
                    return un;
                case TokenType.AS:
                    //return ParseAs(parent, value);
                    break;
                case TokenType.DOT:
                    while (parent.Scanner.Current.Type == TokenType.DOT) {
                        parent.Scanner.Scan();

                        var ma = new MemberAccess();
                        ma.SetParent(parent);
                        value.SetParent(ma);
                        ma.Left = value;
                        ma.Right = EvalExpression(parent);
                        if (ma.Right == parent) {
                            parent.Program.AddError(parent.Scanner.Current, Error.InvalidExpression);
                            return parent;
                        }
                        ma.Right.SetParent(ma);
                        value = ma;
                    }
                    break;
                case TokenType.OPEN_ARRAY:
                    while (parent.Scanner.Current.Type == TokenType.OPEN_ARRAY) {
                        var op = parent.Scanner.Current;
                        parent.Scanner.Scan();
                        var right = EvalExpression(parent);
                        if (parent.Scanner.Current.Type != TokenType.CLOSE_ARRAY) {
                            parent.Program.AddError(parent.Scanner.Current, Error.ExpectingEndOfArray);
                            return value;
                        }
                        parent.Scanner.Scan();
                        value = new Indexer() { Token = op, Left = value, Right = right };
                        value.SetParent(parent);
                    }
                    break;
            }
            return value;
        }

        static ValueType ParseAs(ValueType parent) {
            if (parent.Scanner.Current.Type != TokenType.AS) {
                return parent;
            }
            if (parent.Scanner.Test().Type != TokenType.NAME) {
                parent.Program.AddError(Error.ExpectingName, parent);
                return parent;
            }
            var a = new As();
            a.SetParent(parent);
            a.Left = parent;
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
                    lit.SetParent(parent);
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
            //if (parent.Scanner.Expect('(')) {
            //    var call = new CallerV2();
            //    call.Token = token;
            //    call.From = parent;
            //    call.SetParent(parent.Parent);
            //    parent.SetParent(call);
            //    call.Parse();
            //    return call;
            //}
            //if (parent.Scanner.Expect('[')) {
            //    var array = new ArrayV2();
            //    array.SetParent(parent);
            //    array.Token = token;
            //    if (parent.Scanner.Expect(']') == false) {
            //        array.Parse();
            //    }
            //    return array;
            //}
            if (token.Value == "this") ;
            var id = new Identifier() { Token = token };
            id.SetParent(parent);
            parent.Scanner.Scan();
            return id;
        }

        static ValueType EvalKeyword(ValueType parent, Token token) {
            switch (token.Value) {
                case "as":
                    break;
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
                    if (token.Line == 58) ;
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

    public class Array : Caller {
        public Array() { End = TokenType.CLOSE_ARRAY; }

        public override void Save(TextWriter writer, Builder builder) {
            switch (From) {
                case Var v when v.Parent is Class: writer.Write("this->"); break;
            }
            if (Token != null) writer.Write(Token.Value);
            writer.Write('[');
            SaveValues(writer, builder, false);
            writer.Write(']');
        }
    }

    public class Parameters : List<AST> {
        public Parameters() : base(0) {
        }
    }

    public class Callable : ValueType {
        public AST From;
        public Function Function;
        public Parameters Parameters = new();
        public TokenType End = TokenType.CLOSE_PARENTESES;

        public override void Parse() {
            Parameters = ParseParameters(this, End);
        }

        public static Parameters ParseParameters(AST parent, TokenType end) {
            var list = new Parameters();
            if (parent.Scanner.Test().Type == end) {
                parent.Scanner.Scan();
            } else {
                while (true) {
                    var p = new Expression();
                    p.SetParent(parent);
                    p.Parse();
                    if (p == null) {
                        break;
                    }
                    p.Level = parent.Level + 2;
                    list.Add(p);
                    if (parent.Scanner.Current.Type == end) {
                        break;
                    }
                    if (parent.Scanner.Current.Type != TokenType.COMMA) {
                        parent.Program.AddError(parent.Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                        break;
                    }
                }
            }
            parent.Scanner.Scan();
            return list;
        }

        public override void Print() {
            From?.Print();
            base.Print();
            foreach (var child in Parameters) {
                child.Level = Level + 1;
                child.Print();
            }
        }
    }

    public class Caller : Callable {

        public override void Save(TextWriter writer, Builder builder) {
            if (Function == null) {
                Program.AddError(Error.UnknownName, this);
                return;
            }
            if (Function.IsNative) {
                if (Function.Native != null && Function.Native.Count > 0) {
                    writer.Write(Function.Native[0]);
                } else {
                    writer.Write(Token.Value);
                }
            } else {
                writer.Write(Real);
            }
            writer.Write('(');
            bool started = false;
            if (Function.Access == AccessType.STATIC) {
            } else if (Parent is MemberAccess access) {
                // codigo muito estranho
                if (access.Parent is MemberAccess ma) {
                    ma.Left.Save(writer, builder);
                    started = true;
                }
            } else if (From is Property) {
                writer.Write("this");
                started = true;
            } else if (Function is not Operator && Function.Parent is Class) {
                writer.Write("this");
                started = true;
            }
            SaveValuesOrReplace(writer, builder, started);
            writer.Write(')');
        }

        private void SaveValuesOrReplace(TextWriter writer, Builder builder, bool started) {
            if (Function.IsNative && Function.Native != null && Function.Native.Count > 1 && Function.Parameters != null && Function.Parameters.Children.Count > 0) {
                var args = Function.Native[1];
                Parameter param = null;
                for (int i = 0; i < Function.Parameters.Children.Count || i < Parameters.Count; i++) {
                    if (Function.Parameters.Children.Count > i) {
                        param = Function.Parameters.Children[i] as Parameter;
                    }
                    if (Function.Parameters.Children.Count > i && Parameters.Count > i) {
                        if (args.Contains("$" + param.Token.Value)) {
                            var value = Parameters[i];
                            if (value == null) {
                                args = args.Replace("$" + param.Token.Value, "0");
                            } else {
                                var memory = new StringWriter();
                                value.Save(memory, builder);
                                args = args.Replace("$" + param.Token.Value, memory.ToString());
                            }
                        }
                    } else if (Function.HasVariadic && param != null) {
                        if (Function.Parameters.Children.Count > i) {
                            args = args.Replace("$" + param.Token.Value, "").Trim();
                            if (args.EndsWith(',')) {
                                // remove comma at the end
                                args = args.Substring(0, args.Length - 1);
                            }
                            break;
                        } else if (Parameters.Count > i) {
                            var value = Parameters[i] as ValueType;
                            if (Validator.AreCompatible(value.Type, param.Type) == false) {
                                Function.Program.AddError(value.Token, Error.IncompatibleType);
                                continue;
                            }
                            var memory = new StringWriter();
                            value.Save(memory, builder);
                            args += (i > 0 ? "," : "") + memory.ToString();
                        }
                    }
                }
                writer.Write(args);
            } else {
                SaveValues(writer, builder, started);
            }
        }

        public void SaveValues(TextWriter writer, Builder builder, bool started) {
            if (Parameters != null && Parameters.Count > 0) {
                if (started) {
                    writer.Write(", ");
                }
                for (int i = 0; i < Parameters.Count; i++) {
                    var value = Parameters[i];
                    if (value == null) continue;
                    if (i > 0) {
                        writer.Write(',');
                    }
                    value.Save(writer, builder);
                }
            }
        }

        public override string ToString() {
            return base.ToString() + " (" + Real + ")";
        }
    }
}