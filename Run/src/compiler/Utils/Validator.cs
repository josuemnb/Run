using System;
using System.Linq;
using System.Text;

namespace Run {
    public class Validator(Builder builder) {
        public Builder Builder = builder;

        public void Validate() {
            Validate(Builder.Program);
            ValidateInterfaces();
        }
        void ValidateInterfaces() {
            foreach (var cls in Builder.Classes.Values) {
                if (cls.HasInterfaces == false) return;
                for (int i = 0; i < cls.Interfaces.Count; i++) {
                    var inter = cls.Interfaces[i];
                    for (int j = 0; j < inter.Children.Count; j++) {
                        var child = inter.Children[j];
                        switch (child) {
                            case Function func:
                                var buff = new StringBuilder(cls.Token.Value).Append('_').Append(func.Token.Value);
                                Builder.SetRealName(func, buff);
                                if (cls.FindMember<Function>(buff.ToString()) == null) {
                                    Builder.Program.AddError(func.Token, Error.InterfaceMemberNotFound + " at: " + cls.Token.Value);
                                }
                                break;
                            case Property prop:
                                if (cls.FindMember<Property>(prop.Token.Value) == null) {
                                    Builder.Program.AddError(prop.Token, Error.InterfaceMemberNotFound + " at: " + cls.Token.Value);
                                }
                                break;
                        }
                    }
                }
            }
        }
        private void Validate(Block block) {
            if (block.Validated) return;
            block.Validated = true;
            for (int i = 0; i < block.Children.Count; i++) {
                var item = block.Children[i];
                Validate(item);
            }
        }
        public void Validate(AST ast, bool showError = true) {
            if (ast == null) {
                Builder.Program.AddError(Error.InvalidExpression);
                return;
            }
            if (ast.Validated) return;
            if (ast is not Property && ast is not Indexer && ast is ValueType vt && vt.Type != null && vt.Type.IsTemporary == false) return;
            switch (ast) {
                case Operator op: Validate(op); break;
                case Function f: Validate(f); break;
                case Goto gt: Validate(gt); break;
                case Label lbl: Validate(lbl); break;
                case Property property: Validate(property); break;
                case Switch sw: Validate(sw); break;
                case Base b: Validate(b); break;
                case Indexer idx: Validate(idx); break;
                case Enum @enum: Validate(@enum); break;
                case Var v: Validate(v); break;
                case Else el: Validate(el); break;
                case If If: Validate(If); break;
                case For f: Validate(f); break;
                case Class cls: Validate(cls); break;
                case Delete del: Validate(del); break;
                case Extension ex: break;
                case Defer df: Validate(df); break;
                case Block b: Validate(b); break;
                case Return ret: Validate(ret); break;
                case TypeOf tp: Validate(tp); break;
                case Ref r: Validate(r); break;
                case Scope sc: Validate(sc); break;
                case SizeOf sz: Validate(sz); break;
                case Iterator it: Validate(it); break;
                case ThisExpression t: Validate(t); break;
                case IsExpression @is: Validate(@is); break;
                case RangeExpression range: Validate(range); break;
                case AsExpression a: Validate(a); break;
                case IdentifierExpression id: Validate(id, showError); break;
                case LiteralExpression lit: Validate(lit); break;
                case ArrayCreationExpression ar: Validate(ar); break;
                case IndexerExpression array: Validate(array); break;
                case NewExpression n: Validate(n); break;
                case ConstructorExpression ctor: Validate(ctor); break;
                case CallExpression call: Validate(call); break;
                case TernaryExpression ter: Validate(ter); break;
                case UnaryExpression un: Validate(un); break;
                case DotExpression dot: Validate(dot); break;
                case AssignExpression assign: Validate(assign); break;
                case BinaryExpression bin: Validate(bin); break;
                case ObjectExpression obj: Validate(obj); break;
                case ParentesesExpression p: Validate(p); break;
                case TypeExpression t: Validate(t); break;
                case ContentExpression exp: ValidateExpression(exp); break;
            }
        }

        void Validate(Defer defer) {
            if (defer.Validated) return;
            defer.Validated = true;
            if (defer.Expression != null) {
                Validate(defer.Expression);
                return;
            }
            Validate(defer as Block);
        }

        //void Validate(Extension ex) {
        //    if (ex.Validated) return;

        //    var cls = Builder.Find(ex.Token.Value);
        //    if (cls == null) {
        //        Builder.Program.AddError(ex.Token, Error.UnknownType);
        //        return;
        //    }
        //    Validate(cls);
        //    ex.Type = cls;
        //    Validate(ex as Block);
        //}

        void Validate(AssignExpression bin) {
            if (ValidateBinaryMembers(bin) == false) return;

            if (bin.Left is IdentifierExpression id && id.From is Var v) {
                if (v.IsConst) {
                    Builder.Program.AddError(id.Token, Error.NotPossibleToReassingConstantVariable);
                    return;
                }
            }
            if (AreCompatible(bin.Left, bin.Right) == false) {
                Builder.Program.AddError(bin.Token ?? bin.Right.Token ?? bin.Left.Token, Error.IncompatibleType);
                return;
            }
            switch (bin.Token.Family) {
                case TokenType.LOGICAL:
                    bin.Type = Builder.Bool;
                    return;
                default:
                    bin.Type = bin.Right.Type;
                    return;
            }
        }

        void Validate(IsExpression @is) {
            Validate(@is.Left);
            if (@is.Left is IdentifierExpression id && id.From is Var v) {
                v.NeedRegister = true;
            }
            Validate(@is.Right);
            //@is.Type = @is.Right.Type;
            @is.Type = Builder.Bool;
        }

        void Validate(RangeExpression range) {
            Validate(range as BinaryExpression);
        }

        void Validate(Operator op) {
            Validate(op as Function);
            Validate(op.Type);
        }

        void TransformIteratorRanged(Iterator it) {
            RangeExpression range = it.Content as RangeExpression;
            var f = it.Parent as For;
            (f.Start as Var).Initializer = range.Left;
            f.Condition = new BinaryExpression(f) {
                Token = new Token { Type = TokenType.LOWER, Value = "<" },
                Left = new IdentifierExpression(f) { Token = it.Var.Token, Real = it.Var.Real },
                Right = range.Right,
                Validated = true
            };
            f.Step = new UnaryExpression(f, new IdentifierExpression(f) { Token = it.Var.Token, Real = it.Var.Real }, true) {
                Type = range.Type,
                Validated = true,
                Token = new Token("++") {
                    Scanner = it.Var.Token.Scanner,
                    Line = it.Var.Token.Line,
                }
            };
        }

        void TransformIteratorArray(Iterator it) {
            var f = it.Parent as For;
            //var arr = it.Content as IdentifierExpression;
            var v = new Var() {
                Token = it.Var.Token,
                Type = Builder.Any,
                Real = "_" + it.Var.Token.Value,
                Scanner = it.Scanner,
            };
            v.SetParent(f);
            f.Start = v;
            f.Condition = it;
            //f.Condition = new BinaryExpression(f) {
            //    Token = new Token { Type = TokenType.LOWER, Value = "<" },
            //    Left = new IdentifierExpression(f) { Token = v.Token, Type = Builder.I32 },
            //    Right = new DotExpression(f) {
            //        Left = arr,
            //        Type = Builder.I32,
            //        Right = new IdentifierExpression(f) {
            //            Token = new Token {
            //                Value = "size",
            //                Line = it.Var.Token.Line,
            //                Scanner = it.Var.Token.Scanner
            //            },
            //            Type = Builder.I32,
            //            Validated = true
            //        }
            //    },
            //    Validated = true,
            //};
            //f.Step = new UnaryExpression(f, new IdentifierExpression(f) { Token = it.Var.Token }, true) {
            //    Type = arr.Type,
            //    Validated = true,
            //    Token = new Token("++") {
            //        Scanner = it.Var.Token.Scanner,
            //        Line = it.Var.Token.Line,
            //    }
            //};
        }
        void Validate(Iterator it) {
            if (it.Validated) return;
            it.Validated = true;
            Validate(it.Content);
            var f = it.Parent as For;
            f.Start = it.Var;
            f.Start.SetParent(f);
            if (it.Content is RangeExpression) {
                TransformIteratorRanged(it);
                return;
            }
            if (it.Content is IdentifierExpression idr && idr.Type == Builder.Array) {
                TransformIteratorArray(it);
            }
        }
        void Validate(Function func) {
            if (func.Validated) return;
            if (func.HasGenerics) {
                return;
            }
            Validate(func as Block);
            Validate(func.Type);
        }
        void Validate(Label label) {
            if (label.Validated) return;
            label.Validated = true;
            var func = label.FindParent<Function>();
            foreach (var lbl in func.FindChildren<Label>()) {
                if (lbl == label) continue;
                if (lbl.Token.Value == label.Token.Value) {
                    Builder.Program.AddError(label.Token, Error.NameAlreadyExists);
                    return;
                }
            }
        }
        void Validate(Goto gt) {
            if (gt.Validated) return;
            gt.Validated = true;
            var func = gt.FindParent<Function>();
            foreach (var lbl in func.FindChildren<Label>()) {
                if (lbl.Token.Value == gt.Token.Value) {
                    return;
                }
            }
            Builder.Program.AddError(gt.Token, Error.UnknownName);
        }
        void Validate(Base b) {
            if (b.Validated) return;
            b.Validated = true;
            if (b.Owner == null) {
                return;
            }
            b.Type = b.Owner.BaseType;
            Validate(b.Type);
            AddErrorIfNull(b);
        }
        void Validate(Property property) {
            if (property.Validated) return;
            property.Validated = true;
            if (property.Type.IsTemporary) {
                if (Builder.Classes.TryGetValue(property.Type.Token.Value, out Class type) == false) {
                    Builder.Program.AddError(property.Type.Token, Error.UnknownType);
                    return;
                }
                property.Type = type;
            }
            Validate(property.Type);
            if (property.Getter != null) {
                property.Getter.Type = property.Type;
                Validate(property.Getter);
            }
            if (property.Setter != null) {
                Validate(property.Setter);
            }
            if (property.Initializer != null) {
                Validate(property.Initializer);
            }
            AddErrorIfNull(property);
        }
        void Validate(Switch sw) {
            if (sw.Validated) return;
            sw.Validated = true;
            Validate(sw.Expression);
            foreach (var item in sw.Children) {
                if (item is Case c) {
                    Validate(c);
                    if (AreCompatible(c.Type, sw.Expression.Type) == false) {
                        Builder.Program.AddError(c.Token, Error.IncompatibleType);
                        return;
                    }
                    Validate(c.Type);
                    if (sw.Type == null) {
                        sw.Type = c.Type;
                    } else if (sw.Type != c.Type) {
                        sw.SameType = false;
                    }
                } else if (item is Default d) {
                    Validate(d);
                }
            }
            foreach (var item in sw.Children) {
                if (item is Case c) {
                    c.SameType = sw.SameType;
                } else if (item is Default d) {
                    d.Type = sw.Type;
                    d.SameType = sw.SameType;
                }
            }
        }
        void Validate(Case c) {
            if (c.Validated) return;
            for (int i = 0; i < c.Expressions.Count; i++) {
                var exp = c.Expressions[i];
                Validate(exp);
                if (c.Type == null) {
                    c.Type = exp.Type;
                } else if (c.Type != exp.Type) {
                    c.SameType = false;
                }
            }
            Validate(c as Block);
        }
        void Validate(Enum @enum) {
            if (@enum.Validated) return;
            Class cls = null, found;
            foreach (EnumMember child in @enum.Children) {
                if (child.Content != null) {
                    Validate(child);
                    found = child.Type;
                } else {
                    found = Builder.I32;
                    @enum.IsPrimitive = true;
                }
                if (found == null) continue;
                if (found.IsNumber == false && found != Builder.CharSequence) {
                    Builder.Program.AddError(@enum.Token, Error.IncompatibleType);
                    continue;
                }
                if (cls == null) {
                    cls = found;
                } else if (cls != found) {
                    Builder.Program.AddError(@enum.Token, Error.IncompatibleType);
                }
            }
            Validate(cls);
            @enum.BaseType = cls;
            foreach (EnumMember child in @enum.Children) {
                child.Type = @enum.BaseType;
            }
            @enum.Validated = cls != null;
        }
        void Validate(For f) {
            if (f.Validated) return;
            switch (f.Start) {
                case Expression exp: Validate(exp); break;
                case Var v:
                    if (f.HasRange) {
                        Validate(f.Condition);
                        v.Type = f.Condition.Type;
                    }
                    Validate(v);
                    break;
            }
            if (f.Condition != null && f.HasRange == false) Validate(f.Condition);
            if (f.Step != null) Validate(f.Step);
            Validate(f as Block);
        }
        void Validate(Else e) {
            if (e.Validated) return;
            if (e.Condition != null) Validate(e.Condition);
            Validate(e as Block);
        }
        void Validate(If If) {
            if (If.Validated) return;
            Validate(If.Condition);
            Validate(If as Block);
        }
        void Validate(Delete delete) {
            if (delete.Validated) return;
            delete.Validated = true;
            foreach (var item in delete.Block.Children) {
                if (item.Token.Value == "this") {
                    var cls = item.FindParent<Class>();
                    if (cls == null) {
                        Builder.Program.AddError(item.Token, Error.UnknownName);
                        return;
                    }
                    if (cls.Dispose != null) {
                        Validate(cls.Dispose);
                    }
                } else if (delete.FindName(item.Token.Value) is null) {
                    Builder.Program.AddError(item.Token, Error.UnknownName);
                    return;
                }
                switch (item) {
                    case IdentifierExpression i: Validate(i); break;
                    default:
                        Builder.Program.AddError(delete.Token, Error.UnknownType); break;
                }
            }
        }

        void Validate(Return ret) {
            if (ret.Validated) return;
            ret.Validated = true;
            if (ret.Content == null) {
                return;
            }
            Validate(ret.Content);
        }
        void Validate(Class cls) {
            if (cls == null /*|| cls.HasGenerics*/) return;
            if (cls.Validated) return;
            if (cls.IsEnum) return;
            if (cls.HasGenerics) {
                //var generic = Replacer.ValidateGenerics(cls, Builder);
            }
            Builder.SetRealName(cls);
            if (cls.BaseToken != null) {
                if (Builder.Classes.TryGetValue(cls.BaseToken.Token.Value, out Class based) == false) {
                    Builder.Program.AddError(cls.BaseToken.Token, Error.UnknownType);
                    return;
                }
                cls.BaseType = based;
                Validate(cls.BaseType);
            }
            Validate(cls as Block);
        }
        void Validate(Var var) {
            if (var.Validated) return;
            var.Validated = true;
            if (var.Initializer != null) {
                Validate(var.Initializer);
                if (var.Type == null) {
                    ValidateImplicit(var);
                    var.Type = var.Initializer.Type;
                    if (var.Initializer is CallExpression call) {
                        if (call.Function is null) {
                            call.Program.AddError(var.Token, Error.InvalidExpression);
                            return;
                        }
                        var.TypeArray = call.Function.TypeArray;
                    }
                    return;
                }
            }
            var func = var.FindParent<Function>();
            if (var.FindParent<Class>() is Class c) {
                if (var is Parameter p1 && p1.IsMember) {
                    if (c.FindMember<Var>(var.Token.Value) is Var v) {
                        var.Type = v.Type;
                    }
                } else if (var is not Parameter && c.Children.Exists(c => c != var && c.Token.Value == var.Token.Value)) {
                    if (func != null && func.AccessType == AccessType.INSTANCE) {
                        var.Program.AddError(var.Token, Error.NameAlreadyExists);
                        return;
                    }
                } else if (c.HasGenerics) {
                    if (c.Generics.Find(g => g.Token.Value == var.Type.Token.Value) is Generic gen) {
                        var.Generic = gen;
                        if (func != null) {
                            func.HasGeneric = true;
                        }
                        return;
                    }
                }
            }
            if (var.Type == null || var.Type.Token == null) {
                Builder.Program.AddError(var.Token, Error.UndefinedType);
                return;
            }
            if (Builder.Find(var.Type.Token.Value) is Class cls) {
                var.Type = cls;
                Validate(cls);
                return;
            }
            Builder.Program.AddError(var.Type.Token, Error.UnknownType);
            return;
        }

        private void ValidateImplicit(Var var) {
            if (var.Initializer is not LiteralExpression) return;
            if (var.Initializer.Type == null || var.Initializer.Type.IsNative == false) {
                Builder.Program.AddError(var.Token, "Implicit Annotation only works with native types");
                return;
            }
            if (Builder.Program.Implicits.TryGetValue(var.Initializer.Type.Token.Value, out var ast) == false) return;
            switch (ast) {
                case Constructor ctor:
                    var ne = new NewExpression(var, false) {
                        QualifiedName = ctor.Real,
                        Type = ctor.Type,
                        Token = var.Initializer.Token,
                    };
                    ne.Content = new ConstructorExpression(ne, false) {
                        Token = var.Initializer.Token,
                        Function = ctor,
                        Arguments = [var.Initializer],
                        Type = ctor.Type,
                    };
                    var.Initializer = ne;
                    break;
            }
        }

        public void ValidateExpression(ContentExpression exp) {
            if (exp.Validated) return;
            exp.Validated = true;
            Validate(exp.Content);
            exp.Type = (exp.Content as ValueType)?.Type;
            Validate(exp.Type);
            AddErrorIfNull(exp);
        }
        void Validate(LiteralExpression literal) {
            if (literal.Validated) return;
            literal.Validated = true;
            switch (literal.Token.Type) {
                case TokenType.DOUBLE:
                    literal.Type = Builder.F64;
                    break;
                case TokenType.FLOAT:
                    literal.Type = Builder.F32;
                    break;
                case TokenType.HEX:
                    literal.Type = Builder.I32;
                    break;
                case TokenType.NUMBER:
                    literal.Type = Builder.I32;
                    break;
                case TokenType.QUOTE:
                    literal.Type = Builder.CharSequence;
                    break;
                case TokenType.BOOL:
                    literal.Type = Builder.Bool;
                    break;
                case TokenType.CHAR:
                    literal.Type = Builder.I8;
                    break;
                case TokenType.NULL:
                    literal.Type = Builder.Null;
                    literal.Real = "NULL";
                    break;
                default:
                    literal.Program.AddError(literal.Token, Error.UnknownType);
                    return;
            }
            Validate(literal.Type);
        }
        bool ValidateClassMember(IdentifierExpression id, Class cls) {
            if (cls.FindMember<Var>(id.Token.Value) is Var v) {
                Validate(v.Type);
                id.From = v;
                id.Type = v.Type;
                Replacer.Property(id);
                return true;
            }
            return false;
        }
        bool ValidateEnum(ValueType value, string name) {
            if (Builder.Classes.TryGetValue(name, out Class cls)) {
                if (cls.IsEnum == false) return false;
                var en = cls as Enum;
                Validate(en);
                foreach (EnumMember bin in en.Children) {
                    if (bin.Token.Value == value.Token.Value) {
                        value.Type = cls;
                        Validate(value.Type);
                        return true;
                    }
                }
            }
            return false;
        }
        void Validate(IdentifierExpression id, bool showError = true) {
            if (id.Type != null && id.Type.IsTemporary == false) return;
            if (id.Validated) return;
            id.Validated = true;
            if (id.Token.Value == "this" && id.Parent.FindParent<Class>() is Class t) {
                id.From = t;
                id.Type = t;
                Validate(id.Type);
                return;
            }
            //if (left.IsNative) {
            //    id.Real = id.Token.Value;
            //}
            if (id.Parent is DotExpression dot) {
                var dotType = dot.Type;
                if (dot.Left == id && dot.Parent is DotExpression dp) {
                    dotType = dp.Left.Type;
                }
                if (dot.Left is IdentifierExpression li && li.Type != null && li.Type.IsNative) {
                    //id.Real = id.Token.Value;
                }
                if (dotType != null) {
                    if (ValidateEnum(id, dot.Left.Token.Value)) {
                        return;
                    }
                    if (ValidateClassMember(id, dotType)) {
                        return;
                    }
                }
            }
            if (id.Parent.FindName(id.Token.Value) is ValueType vt) {
                Validate(vt);
                id.From = vt;
                id.Type = vt.Type;
                Replacer.Property(id);
                return;
            }
            if (id.FindParent<Class>() is Class cls) {
                if (ValidateClassMember(id, cls)) {
                    return;
                }
            }
            if (Builder.Find(id.Token.Value/*, out var from*/) is Class c1) {
                //id.From = from;
                id.Type = c1;
                Validate(id.Type);
                return;
            }
            if (showError) Builder.Program.AddError(id.Token, Error.UnknownName);
        }

        void Validate(DotExpression dot) {
            if (dot.Validated) return;
            dot.Validated = true;
            Validate(dot.Left);
            dot.Type = dot.Left.Type;
            Validate(dot.Type);
            Validate(dot.Right);
            if (dot.Right.Type == null) {
                if (dot.Right is CallExpression call && call.Function != null && call.Function.Type == null) {
                    dot.Type = null;
                    return;
                }
                dot.Program.AddError(dot.Right.Token, Error.InvalidExpression);
                return;
            }
            if (dot.Right is IdentifierExpression id && id.From != null && id.From.AccessType == AccessType.STATIC) {
                if (Replacer.Self(dot, dot.Right)) {
                    return;
                }
            }
            dot.Type = dot.Right.Type;
        }

        bool ValidateBinaryMembers(BinaryExpression bin) {
            if (bin.Validated) return false;

            if (bin.Left == null || bin.Right == null) {
                bin.Program.AddError(bin.Token, Error.InvalidExpression);
                return false;
            }
            bin.Validated = true;
            Validate(bin.Left);
            Validate(bin.Right);
            if (ChangeToImplicit(bin.Left) is NewExpression nl) {
                bin.Left = nl;
            }
            if (ChangeToImplicit(bin.Right) is NewExpression nr) {
                bin.Right = nr;
            }
            return true;
        }

        void Validate(BinaryExpression bin) {
            if (ValidateBinaryMembers(bin) == false) return;
            if (bin.Left.Type != null && bin.Left.Type.HasOperators) {
                if (Replacer.Operator(bin, Builder)) return;
            }

            if (AreCompatible(bin.Left, bin.Right) == false) {
                Builder.Program.AddError(bin.Right.Token ?? bin.Left.Token ?? bin.Token, Error.IncompatibleType);
                return;
            }
            switch (bin.Token.Family) {
                case TokenType.LOGICAL:
                    bin.Type = Builder.Bool;
                    return;
                default:
                    bin.Type = bin.Right.Type;
                    return;
            }
        }

        NewExpression ChangeToImplicit(Expression from) {
            if (from.Type == null) {
                return null;
            }
            if (Builder.Program.Implicits.TryGetValue(from.Type.Token.Value, out var imp) && imp is Constructor ctor) {
                Validate(ctor);
                var exp = new NewExpression(from) {
                    QualifiedName = ctor.Real,
                    Type = ctor.Type,
                    Token = ctor.Token,
                    Validated = true,
                };
                exp.Content = new ConstructorExpression(exp, false) {
                    Validated = true,
                    Token = from.Token,
                    Function = ctor,
                    Arguments = [from],
                    Type = ctor.Type,
                };
                return exp;
            }
            return null;
        }

        Function FindInClass(string name, Class cls, bool maybeIsThis = true) {
            var initial = name;
            if (name.StartsWith(cls.Token.Value)) {
                if (maybeIsThis) name = string.Concat(cls.Token.Value, "_this", name.AsSpan(cls.Token.Value.Length));
            } else {
                name = cls.Token.Value + "_" + name;
            }
            var func = GetFunction(name);
            if (func != null) {
                return func;
            }
            if (cls.IsBased) {
                return FindInClass(initial, cls.BaseType);
            }
            return null;
        }

        void Validate(ConstructorExpression ctor) {
            if (ctor.Validated) return;
            ctor.Validated = true;
            var cls = Builder.Find(ctor.Token.Value/*, out var from*/);
            if (cls == null) {
                Builder.Program.AddError(ctor.Token, Error.UnknownType);
                return;
            }
            var real = GetRealName(ctor);
            var func = GetFunction(real);
            if (func == null) {
                Builder.Program.AddError(ctor.Token, Error.UnknownFunctionNameOrWrongParamaters);
                return;
            }
            ctor.Function = func;
            ctor.Real = func.Real;
            ctor.Type = func.Type;
            Validate(ctor.Type);
        }

        static Function FindInClass(CallExpression call, Class cls) {
            if (cls.Children != null) {
                for (int i = 0; i < cls.Children.Count; i++) {
                    var child = cls.Children[i];
                    if (child is Function func) {
                        if (func.Token.Value == call.Token.Value) {
                            if ((func.Parameters?.Children.Count ?? 0) == call.Arguments.Count) {
                                for (int a = 0; a < call.Arguments.Count; a++) {
                                    var arg = call.Arguments[a];
                                    var param = func.Parameters.Children[a] as Parameter;
                                    if (AreCompatible(arg, param) == false) {
                                        goto next;
                                    }
                                }
                                return func;
                            }
                        }
                    }
                next:;
                }
            }
            if (cls.IsBased) {
                return FindInClass(call, cls.BaseType);
            }
            return null;
        }

        void ValidateMemberCall(CallExpression call) {
            Validate(call.Caller);
            if (call.Caller.Type == null) {
                call.Program.AddError(call.Caller.Token, Error.UnknownType);
                return;
            }
            if (call.Caller.Type is Class cls) {
                foreach (var func in cls.FindMembers<Function>(call.Token.Value)) {
                    if ((func.Parameters?.Children.Count ?? 0) == call.Arguments.Count) {
                        for (int a = 0; a < call.Arguments.Count; a++) {
                            var arg = call.Arguments[a];
                            Validate(arg);
                            //maybe a replace happened and now get it again
                            arg = call.Arguments[a];
                            var param = func.Parameters.Children[a] as Parameter;
                            if (AreCompatible(arg, param) == false) {
                                goto next;
                            }
                        }
                        Validate(func);
                        call.Function = func;
                        call.Real = func.Real;
                        call.Type = func.Type;
                        Validate(call.Type);
                        if (call.Caller is Base b) {
                            b.Token.Value = "this";
                        }
                        return;
                    }
                next:;
                }
            }
            if (call.Function == null) {
                call.Program.AddError(call.Token, Error.UnknownFunctionNameOrWrongParamaters);
                return;
            }
        }

        void Validate(CallExpression call) {
            if (call.Validated) return;
            call.Validated = true;
            if (call.Caller != null) {
                ValidateMemberCall(call);
                return;
            }
            var real = GetRealName(call);
            if (real == null) return;
            var func = GetFunction(real);
            if (func == null) {
                if (call.Parent is DotExpression dot && dot.Type != null) {
                    if (FindInClass(call, dot.Type) is Function f) {
                        func = f;
                        goto found;
                    }
                }
                if (call.FindParent<Class>() is Class cls) {
                    if (FindInClass(call, cls) is Function f) {
                        call.Caller = new ThisExpression(call) {
                            Token = new Token { Value = "this" }
                        };
                        func = f;
                        goto found;
                    }
                }
                Builder.Program.AddError(call.Token, Error.UnknownFunctionNameOrWrongParamaters);
                return;
            }
        found:
            call.Function = func;
            call.Type = func.Type;
            call.Real = func.Real;
            Validate(call.Type);
        }
        void Validate(Scope scope) {
            if (scope.Validated) return;
            scope.Validated = true;
            if (Builder.Classes.TryGetValue(scope.Token.Value, out Class cls) == false) {
                Builder.Program.AddError(scope.Token, Error.UnknownType);
                return;
            }
            var ctors = cls.FindChildren<Constructor>().ToList();
            if (ctors.Count > 0) {
                foreach (var ctor in ctors) {
                    if (ctor.Parameters == null || ctor.Parameters.Children.Count == 0) {
                        goto end;
                    }
                }
                Builder.Program.AddError(scope.Token, Error.ScopeOnlyOfConstructorNoParameters);
                return;
            }
        end:
            scope.Type = cls;
            Validate(cls);
        }
        void Validate(SizeOf sizeOf) {
            if (sizeOf.Validated) return;
            sizeOf.Validated = true;
            Validate(sizeOf.Content);
            sizeOf.Type = Builder.I32;
            Validate(sizeOf.Type);
        }
        void Validate(Ref r) {
            if (r.Validated) return;
            r.Validated = true;
            Validate(r.Content);
            r.Type = Builder.Pointer;
        }
        void Validate(NewExpression n) {
            if (n.Validated) return;
            n.Validated = true;
            if (n.HasGenerics) {
                Replacer.Generics(n, Builder);
            } else if (n.Generic != null) {
                //Replacer.Generic(n, Builder);
                return;
            } else {
                if (Builder.Classes.TryGetValue(n.QualifiedName, out Class cls) == false) {
                    Builder.Program.AddError(n.Token, Error.UnknownType);
                    return;
                }
                n.Type = cls;
            }
            Validate(n.Type);
            if (n.Content is ArrayCreationExpression ind) {
                Validate(ind);
                ind.Type = n.Type;
            } else if (n.Content is ConstructorExpression call) {
                Validate(call);
            }
        }
        void Validate(TypeExpression t) {
            if (t.Validated) return;
            t.Validated = true;
            var found = Builder.Find(t.Token.Value);
            if (found == null) {
                Builder.Program.AddError(t.Token, Error.UnknownType);
                return;
            }
            t.Type = found;
            Validate(t.Type);
        }
        void Validate(AsExpression a) {
            if (a.Validated) return;
            a.Validated = true;
            Validate(a.Left);
            Validate(a.Right);
            if (a.Right.Type == null) {
                a.Program.AddError(a.Right.Token, Error.UnknownType);
                return;
            }
            a.Type = a.Right.Type;
            Validate(a.Type);
        }
        void Validate(UnaryExpression un) {
            if (un.Validated) return;
            un.Validated = true;
            Validate(un.Content);
            un.Type = (un.Content as ValueType)?.Type;
            AddErrorIfNull(un);
        }
        void Validate(ThisExpression t) {
            if (t.Validated) return;
            t.Validated = true;
            t.Type = t.FindParent<Class>();
            Validate(t.Type);
            AddErrorIfNull(t);
        }
        void Validate(ParentesesExpression p) {
            if (p.Validated) return;
            p.Validated = true;
            Validate(p.Content);
            p.Type = (p.Content as ValueType)?.Type;
            AddErrorIfNull(p);
        }
        void Validate(IndexerExpression index) {
            if (index.Validated) return;
            index.Validated = true;
            Validate(index.Left);
            if (index.Left.Type == null) {
                index.Program.AddError(index.Left.Token, Error.UnknownType);
                return;
            }
            Validate(index.Right);
            if (index.Left is IdentifierExpression id && id.From is Var v && (v.TypeArray || v.Arguments != null)) {
                index.Type = v.Type;
                Validate(index.Type);
                return;
            }
            if (index.Left.Type.HasIndexers) {
                foreach (var item in index.Left.Type.Children) {
                    if (item is Indexer i) {
                        if (AreCompatible(index.Right, i.Index)) {
                            index.Type = i.Type;
                            Validate(i);
                            Replacer.Indexer(index, i);
                            return;
                        }
                    }
                }
            }
            index.Type = index.Left.Type;
            AddErrorIfNull(index);
        }

        void Validate(Indexer index) {
            if (index.Validated) return;
            index.Validated = true;
            Validate(index.Index);
            Validate(index.Type);
            if (index.Getter != null) {
                index.Getter.Type = index.Type;
                Validate(index.Getter);
            }
            if (index.Setter != null) {
                Validate(index.Setter);
            }
        }

        static void AddErrorIfNull(ValueType value) {
            if (value.Type == null) {
                value.Program.AddError(value.Token, Error.UnknownType);
            }
        }

        void Validate(ArrayCreationExpression array) {
            if (array.Validated) return;
            array.Validated = true;
            Validate(array.Content);
            AddErrorIfNull(array.Content);
        }

        void Validate(TypeOf type) {
            if (type.Validated) return;
            type.Validated = true;
            Validate(type.Content);
            switch (type.Content) {
                case IdentifierExpression id:
                    if (Builder.Classes.TryGetValue(id.Token.Value, out Class cls)) {
                        Validate(cls);
                        type.Type = id.Type = cls;
                        return;
                    }
                    if (id.FindName(id.Token.Value) is ValueType vt) {
                        Validate(vt);
                        type.Type = id.Type = vt.Type;
                        return;
                    }
                    break;
                case ThisExpression t:
                    type.Type = t.Type;
                    return;
                default: {
                        type.Type = null;
                    }
                    break;
            }
        }
        void Validate(TernaryExpression ternary) {
            if (ternary.Validated) return;
            ternary.Validated = true;
            Validate(ternary.Condition);
            if (ternary.Condition.Type == null) {
                ternary.Program.AddError(ternary.Token, Error.UnknownType);
                return;
            }
            if (ternary.Condition.Type.Token.Value != "bool") {
                ternary.Program.AddError(ternary.Token, Error.InvalidExpression);
                return;
            }
            Validate(ternary.True);
            Validate(ternary.False);
            if (AreCompatible(ternary.False, ternary.True) == false) {
                Builder.Program.AddError(ternary.Token, Error.IncompatibleType);
                return;
            }
            ternary.Type = ternary.True.Type;
        }
        Function GetFunction(string name) {
            if (name == null) return null;
            if (Builder.Functions.TryGetValue(name, out Function found)) {
                return found;
            }
            foreach (var f in Builder.Functions.Values) {
                if (f.HasVariadic) {
                    var vname = f.Real[..^9];
                    if (vname.StartsWith(name)) {
                        return f;
                    }
                    if (name.StartsWith(vname)) {
                        return f;
                    }
                }
            }
            return null;
        }
        string GetRealName(CallExpression call) {
            StringBuilder buff = new(call.Token.Value);
            if (call.Parent != null && call.Parent is NewExpression) {
                buff.Append("_this");
            } else if (call.Parent is DotExpression dot && dot.Type != null) {
                buff.Insert(0, '_').Insert(0, dot.Type.Token.Value);
            }
            foreach (ValueType param in call.Arguments.ToArray()) {
                if (param.Type == null) {
                    Validate(param);
                }
                if (param.Type == null) {
                    param.Program.AddError(param.Token, Error.UnknownType);
                    return null;
                }
                buff.Append('_').Append(param.Type.Token.Value);
            }
            return buff.ToString();
        }
        //public static bool AreCompatible(ValueType vt1, ValueType vt2) => AreCompatible(null, vt1, vt2);
        public static bool AreCompatible(ValueType vt1, ValueType vt2) {
            var t1 = vt1?.Type ?? null;
            var t2 = vt2?.Type ?? null;
            if (t1 == t2) return true;
            if (t1 == null || t2 == null) return false;
            if (t1 is Null) {
                if (t2 is Null) return true;
                if (t2.IsPrimitive == false) return true;
                if (vt2 is IdentifierExpression id && id.From is Var v && v.TypeArray) {
                    return true;
                }
            }
            if (t2 is Null) {
                if (t1.IsPrimitive == false) return true;
                if (vt1 is IdentifierExpression id && id.From is Var v && v.TypeArray) {
                    return true;
                }
            }
            if (t1.IsNumber && t2.IsNumber) {
                return true;
            }
            if (t1.Token.Value == t2.Token.Value) {
                return true;
            }

            return false;
        }
        public static bool AreCompatible(Class t1, Class t2) {
            if (t1 is null && t2 != null && t2.IsPrimitive == false) return true;
            if (t2 is null && t1 != null && t1.IsPrimitive == false) return true;
            if (t1 == null || t2 == null) return false;
            if (t1 == t2) return true;
            if (t1.IsAny || t2.IsAny) return true;
            if (t1.IsNumber && t2.IsNumber) return true;
            if (t1.Token.Value == t2.Token.Value) return true;
            return false;
        }
    }
}
