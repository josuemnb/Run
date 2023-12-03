using System;
using System.IO;

namespace Run.V12 {
    public class Property : Var {

        public enum PropertyKind {
            None = 0,
            Getter = 1,
            Setter = 2,
            Initializer = 4,
        }

        public Function Getter;
        public Function Setter;

        public PropertyKind SimpleKind;

        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.Expect(':') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingDeclare);
                return;
            }
            if (GetName(out Token type) == false) return;
            Type = new Class {
                Token = type
            };
            switch (Scanner.Test().Value) {
                case "=": Scanner.Scan(); ParseInitializer(); break;
                case "{": Scanner.Scan(); ParseAll(); break;
                default: ParseGetter(); break;
            }
        }

        void ParseAll() {
        again:
            Scanner.Skip(true);
            var test = Scanner.Test();
            switch (test.Type) {
                case TokenType.CLOSE_BLOCK:
                    Scanner.Scan();
                    return;
                case TokenType.EOL:
                    Scanner.Scan();
                    goto again;
                case TokenType.EOF:
                    Scanner.Scan();
                    return;
                case TokenType.NAME:
                    switch (test.Value) {
                        case "get":
                            if (Getter != null) {
                                Program.AddError(Scanner.Current, "Duplicate 'get' in property");
                                Scanner.SkipBlock();
                                return;
                            }
                            Scanner.Scan();
                            ParseGetter();
                            break;
                        case "set":
                            if (Setter != null) {
                                Program.AddError(Scanner.Current, "Duplicate 'set' in property");
                                Scanner.SkipBlock();
                                return;
                            }
                            Scanner.Scan();
                            ParseSetter();
                            break;
                        default:
                            Program.AddError(Scanner.Current, "Expecting 'get' or 'set'");
                            Scanner.SkipBlock();
                            return;
                    }
                    goto again;
                default:
                    Program.AddError(Scanner.Current, "Expecting 'get' or 'set'");
                    break;
            }
        }

        void ParseSetter() {
            Setter = new Function();
            Setter.SetParent(this);
            Parse(ref Setter, true);
        }

        void ParseGetter() {
            Getter = new Function();
            Getter.SetParent(this);
            Parse(ref Getter, false);
        }

        void Parse(ref Function func, bool setter) {
            bool block = false;
            bool simple = false;
            switch (Scanner.Test().Value) {
                case "{":
                    Scanner.Scan();
                    block = true;
                    break;
                case "=>":
                    Scanner.Scan();
                    break;
                case ";":
                    Scanner.Scan();
                    simple = true;
                    SimpleKind |= setter ? PropertyKind.Setter : PropertyKind.Getter;
                    //break;
                    return;
                default:
                    Program.AddError(Scanner.Current, Error.ExpectingArrowOfBeginOfBlock);
                    return;
            }
            func.Token = Token.Clone();
            func.Token.Value += "_" + (setter ? "set" : "get");
            if (setter) {
                func.Parameters = func.Add<Block>();
                func.Parameters.Add(new Parameter {
                    Token = new Token {
                        Value = "value",
                    },
                    Type = Type
                });
            } else {
                func.Type = Type;
            }
            if (block == true) {
                func.ParseBlock();
                return;
            }
            if (setter) {
                FinishSetter(func, simple);
                return;
            }
            FinishGetter(func, simple);
        }

        void FinishGetter(Function func, bool simple) {
            var ret = func.Add<Return>();
            if (simple) {
                ret.Expression = new Expression();
                ret.Expression.SetParent(ret);
                var id = new Identifier {
                    Virtual = true,
                    Token = Token,
                    Type = Type,
                };
                id.SetParent(ret.Expression);
                ret.Expression.Result = id;
                return;
            }
            ret.Parse();
        }

        void FinishSetter(Function func, bool simple) {
            var exp = func.Add<Expression>();
            if (simple) {
                var bin = new Binary {
                    Type = Type,
                    Token = new Token {
                        Value = "=",
                        Scanner = Scanner,
                        Type = TokenType.ASSIGN,
                        Family = TokenType.ARITMETIC,
                    },
                    Left = new Identifier {
                        Virtual = true,
                        Token = Token,
                        Type = Type,
                    },
                    Right = new Identifier {
                        Token = new Token {
                            Value = "value",
                            Scanner = Scanner,
                            Type = TokenType.NAME,
                        },
                        Type = Type,
                    },
                };
                bin.SetParent(exp);
                bin.Left.SetParent(bin);
                bin.Right.SetParent(bin);
                exp.Result = bin;
                return;
            }
            exp.Parse();
            return;
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Initializer != null) {
                base.Save(writer, builder);
                return;
            }
            Getter?.Save(writer, builder);
            Setter?.Save(writer, builder);
        }

        public override void Print() {
            base.Print();
            Getter?.Print();
            Setter?.Print();
        }
    }
}