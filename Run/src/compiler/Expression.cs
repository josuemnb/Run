using System;
using System.Collections.Generic;
using System.IO;

namespace Run {
    public class ValueType : AST {
        public Class Type;
        public bool IsNull;
    }
    public class Expression_ : ValueType {
        public AST Result;
        static Expression_ Current;
        public bool HasError { get; private set; }
        public Expression_() { }
        internal enum PrecedenceLevel {
            Assignment,
            Ternary,
            LogicalOR,
            LogicalAND,
            BitwiseOR,
            BitwiseXOR,
            BitwiseAND,
            Equality,
            Relational,
            Shift,
            Additive,
            Multiplicative,
            Unary,
            //MemberAcess,
            Postfix,
        }
        static readonly Dictionary<TokenType, PrecedenceLevel> Precedences = new() {
            { TokenType.ASSIGN, PrecedenceLevel.Assignment },
            { TokenType.PLUS_ASSIGN, PrecedenceLevel.Assignment },
            { TokenType.MINUS_ASSIGN, PrecedenceLevel.Assignment },
            { TokenType.MULTIPLY_ASSIGN, PrecedenceLevel.Assignment },
            { TokenType.DIVIDE_ASSIGN, PrecedenceLevel.Assignment },
            { TokenType.EQUAL, PrecedenceLevel.Equality },
            { TokenType.NOT_EQUAL, PrecedenceLevel.Equality },
            { TokenType.NOT, PrecedenceLevel.Unary },
            { TokenType.INCREMENT, PrecedenceLevel.Unary },
            { TokenType.DECREMENT, PrecedenceLevel.Unary },
            { TokenType.BANG, PrecedenceLevel.Unary },
            { TokenType.AND, PrecedenceLevel.LogicalAND },
            { TokenType.OR, PrecedenceLevel.LogicalOR },
            { TokenType.LOWER_OR_EQUAL, PrecedenceLevel.Relational },
            { TokenType.LOWER, PrecedenceLevel.Relational },
            { TokenType.GREATHER, PrecedenceLevel.Relational },
            { TokenType.GREAT_OR_EQUAL, PrecedenceLevel.Relational },
            { TokenType.PLUS, PrecedenceLevel.Additive },
            { TokenType.MINUS, PrecedenceLevel.Additive },
            { TokenType.MULTIPLY, PrecedenceLevel.Multiplicative },
            { TokenType.DIVIDE, PrecedenceLevel.Multiplicative },
            { TokenType.MOD, PrecedenceLevel.Multiplicative },
            { TokenType.DOT, PrecedenceLevel.Postfix },
            { TokenType.OPEN_PARENTESES, PrecedenceLevel.Postfix },
            { TokenType.OPEN_ARRAY, PrecedenceLevel.Postfix }
        };

        static readonly Dictionary<TokenType, Func<AST, AST>> AditionalsPrefixes = new() {
            { TokenType.LOWER, ParseUnary },
            { TokenType.LOWER_OR_EQUAL, ParseUnary },
            { TokenType.DIFFERENT, ParseUnary },
            { TokenType.EQUAL, ParseUnary },
            { TokenType.GREATHER, ParseUnary },
            { TokenType.GREAT_OR_EQUAL, ParseUnary },
        };
        static readonly Dictionary<TokenType, Func<AST, AST>> Prefixes = new() {
            { TokenType.NAME, ParseIdentifier },
            { TokenType.NUMBER, ParseLiteral },
            { TokenType.REAL, ParseLiteral },
            { TokenType.QUOTE, ParseLiteral },
            { TokenType.CHAR, ParseLiteral },
            { TokenType.NOT, ParseBang },
            { TokenType.MINUS, ParseUnary },
            { TokenType.INCREMENT, ParseUnary },
            { TokenType.DECREMENT, ParseUnary },
            { TokenType.OPEN_PARENTESES, ParseParenteses },
        };

        static readonly Dictionary<TokenType, Func<AST, AST, AST>> Sufixes = new() {
            { TokenType.ASSIGN, ParseAssign },
            { TokenType.MINUS, ParseBinary },
            { TokenType.TERNARY, ParseTernary },
            { TokenType.MINUS_ASSIGN, ParseBinary },
            { TokenType.PLUS_ASSIGN, ParseBinary },
            { TokenType.MULTIPLY_ASSIGN, ParseBinary },
            { TokenType.DIVIDE_ASSIGN, ParseBinary },
            { TokenType.PLUS, ParseBinary },
            { TokenType.INCREMENT, ParseUnary },
            { TokenType.DECREMENT, ParseUnary },
            { TokenType.OR, ParseComparation },
            { TokenType.DIFFERENT, ParseComparation },
            { TokenType.NOT, ParseComparation },
            { TokenType.AND, ParseComparation },
            { TokenType.GREATHER, ParseComparation },
            { TokenType.GREAT_OR_EQUAL, ParseComparation },
            { TokenType.LOWER, ParseComparation },
            { TokenType.LOWER_OR_EQUAL, ParseComparation },
            { TokenType.DIVIDE, ParseBinary },
            { TokenType.MOD, ParseBinary },
            { TokenType.MULTIPLY, ParseBinary },
            { TokenType.EQUAL, ParseBinary },
            { TokenType.OPEN_PARENTESES, ParseCall },
            { TokenType.DOT, ParseDot },
            { TokenType.OPEN_ARRAY, ParseArray },
            { TokenType.AS, ParseAs },
        };

        internal static AST ParseAs(AST parent, AST left) {
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
            return a;
        }

        internal static AST ParseParenteses(AST parent) {
            var parenteses = new Parenteses();
            parenteses.SetParent(parent);
            parenteses.Scanner.Scan();
            parenteses.Expression = ParseExpression(parenteses, PrecedenceLevel.Assignment);
            if (parent.Scanner.Expect(')') == false) {

            }
            return parenteses;
        }
        internal static AST ParseUnary(AST parent) {
            var bin = new Unary() {
                Token = parent.Scanner.Current,
                AtRight = false,
            };
            bin.SetParent(parent);
            bin.Scanner.Scan();
            bin.Right = ParseExpression(bin, PrecedenceLevel.Assignment);
            bin.Right?.SetParent(bin);
            return bin;
        }
        internal static AST ParseUnary(AST parent, AST Left) {
            var bin = new Unary(Left) {
                Token = parent.Scanner.Current,
                AtRight = true,
            };
            bin.SetParent(parent);
            bin.Right.SetParent(bin);
            return bin;
        }
        internal static AST ParseBang(AST parent) {
            throw new NotImplementedException();
        }
        internal static AST ParseLiteral(AST parent) {
            var lit = new Literal() { Token = parent.Scanner.Current };
            lit.SetParent(parent);
            return lit;
        }

        internal static AST ParseKeyword(AST parent, Token token) {
            switch (token.Value) {
                //case "length" when parent is MemberAccess:
                //    var len = new Length();
                //    len.SetParent(parent);
                //    return len;
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
                    var cls = Current.FindParent<Class>();
                    if (cls == null) {
                        Current.Program.AddError(token, Error.UnknownName);
                        return null;
                    }
                    var t = new This();
                    t.Token = token;
                    t.Type = cls;
                    t.SetParent(parent);
                    return t;
                case "null":
                    var nu = new Null {
                        Token = token,
                    };
                    nu.SetParent(parent);
                    return nu;
                case "typeof":
                    var ty = new TypeOf();
                    ty.SetParent(parent);
                    ty.Parse();
                    return ty;
            }
            return null;
        }
        internal static AST ParseIdentifier(AST parent) {
            var token = parent.Scanner.Current;
            if (ParseKeyword(parent, token) is AST ret) return ret;
            if (ParseParentesesOrBrackets(parent, token) is AST parsed) return parsed;

            var id = new Identifier() { Token = token };
            id.Token = token;
            id.SetParent(parent);
            return id;
        }
        internal static AST ParseParentesesOrBrackets(AST parent, Token token, AST left = null) {
            if (parent.Scanner.Expect('(')) {
                var caller = ParseParameters<Caller>(parent, left, TokenType.CLOSE_PARENTESES, token);
                if (parent.Scanner.Expect('(')) {
                    parent.Program.AddError(parent.Scanner.Current, Error.InvalidExpression);
                }
                return caller;
            }
            if (parent.Scanner.Expect('[')) {
                var array = ParseParameters<Array>(parent, left, TokenType.CLOSE_ARRAY, token);
                if (parent.Scanner.Expect('[')) {
                    parent.Program.AddError(parent.Scanner.Current, Error.DoubleArrayNotSupported);
                }
                return array;
            }
            return null;
        }

        internal static AST ParseTernary(AST parent, AST left) {
            var ternary = new Ternary(left);
            ternary.SetParent(parent);
            ternary.Parse();
            return ternary;
        }

        internal static AST ParseComparation(AST parent, AST left) => ParseBinary<Comparation>(parent, left);

        internal static AST ParseAssign(AST parent, AST left) => ParseBinary<Assign>(parent, left);
        internal static AST ParseBinary(AST parent, AST left) => ParseBinary<Binary>(parent, left);
        internal static AST ParseDot(AST parent, AST left) => ParseMember(parent, left);

        internal static MemberAccess ParseMember(AST parent, AST left) {
            var access = new MemberAccess() {
                This = left,
                Token = parent.Scanner.Current,
            };
            access.SetParent(parent);
            access.This.SetParent(access);
            parent.Scanner.Scan();
            access.Member = ParseExpression(access, PrecedenceLevel.Postfix);
            if (access.Member == null) {
                return null;
            }
            access.Member.SetParent(access);
            access.End = access.Scanner.Position;
            return access;
        }
        internal static T ParseBinary<T>(AST parent, AST left) where T : Binary, new() {
            var level = PeekPrecedence(parent.Scanner.Current.Type);
            var bin = new T() {
                Left = left,
                Token = parent.Scanner.Current,
            };
            bin.SetParent(parent);
            bin.Left.SetParent(bin);
            parent.Scanner.Scan();
            bin.Right = ParseExpression(bin, level);
            if (bin.Right == null) {
                return null;
            }
            bin.Right.SetParent(bin);
            bin.End = bin.Scanner.Position;
            return bin;
        }
        internal static AST ParseArray(AST parent, AST left) {
            var ret = ParseParameters<Array>(parent, left, TokenType.CLOSE_ARRAY, left.Token);
            return ret;
        }
        internal static AST ParseCall(AST parent, AST left) => ParseParameters<Caller>(parent, left, TokenType.CLOSE_PARENTESES);
        internal static T ParseParameters<T>(AST parent, AST left, TokenType end, Token token = null) where T : Caller, new() {
            var caller = new T() { From = left, Token = token, Values = new(0), };
            caller.SetParent(parent);
            parent.Scanner.Scan();
            while (true) {
                var p = ParseExpression(caller, PrecedenceLevel.Assignment);
                if (p == null) {
                    break;
                }
                p.Level = caller.Level + 2;
                caller.Values.Add(p);
                parent.Scanner.Scan();
                if (parent.Scanner.Current.Type != TokenType.COMMA || parent.Scanner.Current.Type == end) {
                    break;
                }
                parent.Scanner.Scan();
            }
            caller.End = parent.Scanner.Position;
            if (parent.Scanner.Peek() == '.') {

            }
            return caller;
        }
        internal static AST ParseExpression(AST ast, PrecedenceLevel level, bool useAditionals = false) {
            Func<AST, AST> prefix = null;
            if (useAditionals && AditionalsPrefixes.TryGetValue(ast.Scanner.Current.Type, out var aditional)) {
                prefix = aditional;
            } else if (Prefixes.TryGetValue(ast.Scanner.Current.Type, out var p)) {
                prefix = p;
            }
            if (prefix == null) {
                return null;
            }
            AST left = prefix(ast);
            if (left == null) {
                return null;
            }
            while (true) {
                var peek = ast.Scanner.Test();
                var levelPeek = PeekPrecedence(peek.Type);
                if (level > levelPeek) {
                    break;
                }
                if (Sufixes.TryGetValue(peek.Type, out var sufix) == false) {
                    break;
                }
                ast.Scanner.Scan();
                left = sufix(ast, left);
                if (left == null) {
                    break;
                }
            }
            return left;
        }

        public void Parse(bool useAditionals) {
            Scanner.Scan();
            Current = this;
            Result = ParseExpression(this, PrecedenceLevel.Assignment, useAditionals);
            if (Result == null) {
                this.Program.AddError(Scanner.Current, Error.InvalidExpression);
                HasError = true;
            }
            Current = null;
        }
        public override void Parse() => Parse(false);
        public override void Print() {
            base.Print();
            if (Result != null) {
                Result.Level = Level + 1;
                Result.Print();
            }
        }

        public override void Save(TextWriter writer, Builder builder) => Save(Result, writer, builder);
        void Save(AST ast, TextWriter writer, Builder builder) {
            switch (ast) {
                case Literal lit: writer.Write(lit.Token.Value); break;
                default: ast.Save(writer, builder); break;
            }
        }
        static PrecedenceLevel PeekPrecedence(TokenType type) {
            if (Precedences.TryGetValue(type, out var level)) {
                return level;
            }
            return PrecedenceLevel.Assignment;
        }

        public void Replace(string initial, string other) {
            Replace(Result, initial, other);
        }

        void Replace(AST ast, string initial, string other) {
            if (ast.Token != null && ast.Token.Value == initial) {
                ast.Token.Value = other;
            }
            switch (ast) {
                case New n:
                    //n.Expression_.Replace(initial, other);
                    break;
                case Parenteses p:
                    Replace(p.Expression, initial, other);
                    break;
                case Binary bin:
                    Replace(bin.Left, initial, other);
                    Replace(bin.Right, initial, other);
                    break;
                case Caller call:
                    foreach (var param in call.Values) {
                        Replace(param, initial, other);
                    }
                    break;
            }
        }
    }
    public class This : ValueType {

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("this");
        }
    }
    public class Indexer : Binary {

    }
    public class Literal : ValueType {
        public override string ToString() {
            return base.ToString() + " " + Token.Value;
        }
    }
    public class Ternary : ValueType {
        public AST Condition;
        public ExpressionV2 IsTrue, IsFalse;

        public Ternary(AST condition) {
            Condition = condition;
        }

        public override void Parse() {
            Condition.SetParent(this);
            IsTrue = new ExpressionV2();
            IsTrue.SetParent(this);
            IsTrue.Parse();
            if (Scanner.Current.Type != TokenType.DECLARE) {
                Program.AddError(Error.ExpectingAssign, this);
                return;
            }
            IsFalse = new ExpressionV2();
            IsFalse.SetParent(this);
            IsFalse.Parse();
        }

        public override void Save(TextWriter writer, Builder builder) {
            Condition.Save(writer, builder);
            writer.Write("?");
            IsTrue.Save(writer, builder);
            writer.Write(':');
            IsFalse.Save(writer, builder);
        }
    }
    public class Unary : ValueType {
        public AST Right;
        public bool AtRight;
        public Unary(AST right) {
            Right = right;
        }

        public Unary() { }

        public override void Save(TextWriter writer, Builder builder) {
            if (AtRight) {
                Right.Save(writer, builder);
                base.Save(writer, builder);
            } else {
                base.Save(writer, builder);
                Right.Save(writer, builder);
            }
        }

    }
    public class MemberAccess : ValueType {
        public int End;
        public AST This;
        public AST Member;
        public override void Save(TextWriter writer, Builder builder) {
            switch (Member) {
                case Array array:
                    array.Save(writer, builder);
                    break;
                case MemberAccess access:
                    This.Save(writer, builder);
                    writer.Write("->");
                    access.Save(writer, builder);
                    break;
                case Caller call:
                    writer.Write(call.Real);
                    writer.Write('(');
                    if (call.Function.Access == AccessType.STATIC) {
                        call.SaveValues(writer, builder, false);
                    } else {
                        if (Parent is MemberAccess) {
                            var buffer = new StringWriter();
                            WriteParentAcess(this, buffer, builder);
                            writer.Write(buffer.ToString());
                            if (This is Identifier id) {
                                id.From = null;
                            }
                        }
                        This.Save(writer, builder);
                        call.SaveValues(writer, builder, true);
                    }
                    writer.Write(')');
                    break;
                case Identifier id:
                    if (id.From is EnumMember en) {
                        en.Save(writer, builder);
                        break;
                    }
                    if (Parent is MemberAccess) {
                        writer.Write(This.Token.Value);
                    } else {
                        This.Save(writer, builder);
                    }
                    writer.Write("->");
                    writer.Write(id.Token.Value);
                    //id.Save(writer, builder);
                    break;
                default:
                    This.Save(writer, builder);
                    if (Member is not null) {
                        writer.Write("->");
                        Member.Save(writer, builder);
                    }
                    break;
            }
        }

        void WriteParentAcess(AST ast, TextWriter writer, Builder builder) {
            if (ast.Parent is MemberAccess pa) {
                WriteParentAcess(pa, writer, builder);
                pa.This.Save(writer, builder);
                writer.Write("->");
            }
        }

        public override void Print() {
            base.Print();
            This.Level = Level + 1;
            Member.Level = Level + 1;
            This.Print();
            Member.Print();
        }

        public override string ToString() {
            return This + "." + Member;
        }
    }
    public class Identifier : ValueType {
        public AST From;
        public bool Virtual;
        public override void Save(TextWriter writer, Builder builder) {
            if (From is Field f) {
                if (f.Access == AccessType.STATIC) {
                    writer.Write(f.Real);
                    return;
                }
                writer.Write("this->");
            } else if (Virtual) {
                writer.Write("this->");
            }
            base.Save(writer, builder);
        }

        void AddCheck(TextWriter writer, string type, string value, Token token) {
            writer.Write("CHECK_");
            writer.Write(type);
            writer.Write("(");
            writer.Write(value);
            writer.Write(", ");
            writer.Write(token.Line);
            writer.Write(", \"");
            writer.Write(token.Scanner.Path);
            writer.Write("\")->");
        }

        public override string ToString() {
            return base.ToString() + " " + Token.Value;
        }
    }

    public class Scope : ValueType {
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                Scanner.SkipLine();
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("SCOPE(");
            writer.Write(Token.Value);
            writer.Write(")");
        }
    }
    public class Array : Caller {

        public override void Parse() {
            Values = new(1);
            var exp = new ExpressionV2();
            exp.SetParent(this);
            Scanner.Scan();
            exp.Parse();
            Values.Add(exp);
        }

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

    public class PropertySetter : Caller {
        public int Back;
        public AST This;

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Real);
            writer.Write('(');
            if (This != null) {
                This.Save(writer, builder);
            }
            SaveValues(writer, builder, This != null);
            writer.Write(')');
        }
    }
    public class Caller : ValueType {
        public AST From;
        public Function Function;
        public List<AST> Values;
        public int End;

        public override void Print() {
            From?.Print();
            base.Print();
            foreach (var child in Values) {
                child.Level = Level + 1;
                child.Print();
            }
        }

        public override string ToString() {
            return base.ToString() + " (" + Real + ")";
        }

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
                    ma.This.Save(writer, builder);
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
                for (int i = 0; i < Function.Parameters.Children.Count || i < Values.Count; i++) {
                    if (Function.Parameters.Children.Count > i) {
                        param = Function.Parameters.Children[i] as Parameter;
                    }
                    if (Function.Parameters.Children.Count > i && Values.Count > i) {
                        if (args.Contains("$" + param.Token.Value)) {
                            var value = Values[i];
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
                        } else if (Values.Count > i) {
                            var value = Values[i] as ValueType;
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
            if (Values != null && Values.Count > 0) {
                if (started) {
                    writer.Write(", ");
                }
                for (int i = 0; i < Values.Count; i++) {
                    var value = Values[i];
                    if (value == null) continue;
                    if (i > 0) {
                        writer.Write(',');
                    }
                    value.Save(writer, builder);
                }
            }
        }
    }
    public class Parenteses : ValueType {
        public AST Expression;
        public Parenteses() { }

        public override void Print() {
            base.Print();
            if (Expression != null) {
                Expression.Level = Level + 1;
                Expression.Print();
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write('(');
            Expression.Save(writer, builder);
            writer.Write(')');
        }
    }
    public class Ref : ValueType {
        public ExpressionV2 Expression;
        public override void Parse() {
            var parenteses = Scanner.Expect('(');
            Expression = new ExpressionV2();
            Expression.SetParent(this);
            Expression.Parse();
            if (parenteses && Scanner.Expect(')') == false) {
                Program.AddError(Token, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Expression.Type.IsPrimitive) {
                writer.Write("&(");
            } else
            if (Expression.Type.IsNumber) {
                writer.Write("&(");
            } else {
                writer.Write("*(");
            }
            Expression.Save(writer, builder);
            writer.Write(')');
        }
    }
    public class SizeOf : ValueType {
        //public Class Of;
        public ExpressionV2 Expression;
        public override void Parse() {
            var parenteses = Scanner.Expect('(');
            Expression = new ExpressionV2();
            Expression.SetParent(this);
            Expression.Parse();
            if (parenteses && Scanner.Expect(')') == false) {
                Program.AddError(Token, Error.ExpectingCloseParenteses);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Expression.Result is Identifier id) {
                if (id.From is Var p && p.Arrays != null) {
                    writer.Write("SIZEOF(");
                    writer.Write(id.Token.Value);
                    writer.Write(")");
                    return;
                } else if (id.Type != null) {
                    writer.Write("sizeof(");
                    writer.Write(id.Type.Real);
                    writer.Write(')');
                    return;
                }
            } else if (Expression.Result is This) {
                writer.Write("SIZEOF(this)");
                return;
            }
            writer.Write("sizeof(");
            Expression.Save(writer, builder);
            writer.Write(')');
        }
    }
    public class Condition : ValueType { }
    public class Step : ValueType { }
    public class Cast : ValueType {
        public ExpressionV2 Expression;
        public int Arrays { get; private set; }
        public override void Parse() {
            if (Scanner.Expect('(') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingOpenParenteses);
                Scanner.SkipLine();
                return;
            }
            if (GetName(out Token) == false) {
                Scanner.SkipLine();
                return;
            }
            if (Scanner.Expect('[')) {
                if (Scanner.Expect(']') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
                    Scanner.SkipLine();
                    return;
                }
                Arrays++;
            }
            if (Scanner.Expect(',') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                Scanner.SkipLine();
                return;
            }
            Expression = new ExpressionV2();
            Expression.SetParent(this);
            Expression.Parse();
            if (Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
                Scanner.SkipLine();
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Type == null) return;
            writer.Write("CAST(");
            writer.Write(Type.Real);
            if (Type.IsPrimitive == false) {
                writer.Write('*');
            }
            for (int i = 0; i < Arrays; i++) writer.Write("*");
            writer.Write(", ");
            Expression.Save(writer, builder);
            writer.Write(')');
        }
    }
}
