namespace Run {
    public class GetterSetter : Var {
        public Function Getter;
        public Function Setter;
        public PropertyKind SimpleKind;

        internal void ParseAll() {
        again:
            Scanner.Skip(true);
            var test = Scanner.Test();
            switch (test.Type) {
                case TokenType.CLOSE_BLOCK:
                    Scanner.Scan();
                    return;
                case TokenType.EOL:
                    Program.LinesCompiled++;
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
                            if (Getter != null) {
                                Program.AddError(Scanner.Current, "Duplicate 'get' in property");
                                Scanner.SkipBlock();
                                return;
                            }
                            ParseGetter(true);
                            return;
                            //Program.AddError(Scanner.Current, "Expecting 'get' or 'set'");
                            //Scanner.SkipBlock();
                            //return;
                    }
                    goto again;
                default:
                    Program.AddError(Scanner.Current, "Expecting 'get' or 'set'");
                    break;
            }
        }

        internal void ParseSetter() {
            Setter = new Function();
            Setter.SetParent(this);
            Parse(Setter, true);
        }

        internal void ParseGetter(bool parsed = false) {
            Getter = new Function();
            Getter.SetParent(this);
            Parse(Getter, false, parsed);
        }

        internal void Parse(Function func, bool setter, bool parsed = false) {
            bool block = parsed;
            if (parsed == false) {
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
                        SimpleKind |= setter ? PropertyKind.Setter : PropertyKind.Getter;
                        return;
                    default:
                        Program.AddError(Scanner.Current, Error.ExpectingArrowOfBeginOfBlock);
                        return;
                }
            }
            func.Token = Token.Clone();
            func.Token.Value += "_" + (setter ? "set" : "get");
            if (setter) {
                SetSetter(func);
            } else {
                SetGetter(func);
            }
            if (block == true) {
                func.ParseBlock();
                return;
            }
            if (setter) {
                FinishSetter(func, false);
                return;
            }
            FinishGetter(func, false);
        }

        public virtual void SetGetter(Function func) {
            func.Type = Type;
        }

        public virtual void SetSetter(Function func) {
            func.Parameters = func.Add<Block>();
            func.Parameters.Add(new Parameter {
                Token = new Token {
                    Value = "value",
                },
                Type = Type
            });
        }

        internal void FinishGetter(Function func, bool simple) {
            var ret = func.Add<Return>();
            if (simple) {
                ret.Content = new IdentifierExpression(ret) {
                    Virtual = true,
                    Token = Token,
                    Type = Type,
                };
                return;
            }
            ret.Parse();
        }

        internal void FinishSetter(Function func, bool simple) {
            if (simple) {
                func.Add(new BinaryExpression(func) {
                    Type = Type,
                    Token = new Token {
                        Value = "=",
                        Scanner = Scanner,
                        Type = TokenType.ASSIGN,
                        Family = TokenType.ARITMETIC,
                    },
                    Left = new IdentifierExpression(func) {
                        Virtual = true,
                        Token = Token,
                        Type = Type,
                    },
                    Right = new IdentifierExpression(func) {
                        Token = new Token {
                            Value = "value",
                            Scanner = Scanner,
                            Type = TokenType.NAME,
                        },
                        Type = Type,
                    },
                });
                //exp.Result = bin;
                return;
            }
            //var exp = func.Add<Expression>();
            //exp.Parse();
            func.Add(ExpressionHelper.Expression(func));
        }
    }

}