using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
                if (cls.HasInterfaces) {
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
            if (ast.Validated) return;
            if (ast is not Property && ast is ValueType vt && vt.Type != null && vt.Type.IsTemporary == false) return;
            switch (ast) {
                case Function f: Validate(f); break;
                case Goto gt: Validate(gt); break;
                case Label lbl: Validate(lbl); break;
                case Property property: Validate(property); break;
                case Switch sw: Validate(sw); break;
                case Base b: Validate(b); break;
                case Enum @enum: Validate(@enum); break;
                case Var v: Validate(v); break;
                case Else el: Validate(el); break;
                case If If: Validate(If); break;
                case For f: Validate(f); break;
                case Class cls: Validate(cls); break;
                case Delete del: Validate(del); break;
                case Block b: Validate(b); break;
                case Return ret: Validate(ret); break;
                case TypeOf tp: Validate(tp); break;
                case Ref r: Validate(r); break;
                case Scope sc: Validate(sc); break;
                case SizeOf sz: Validate(sz); break;
                case Iterator it: Validate(it); break;
                case ThisExpression t: Validate(t); break;
                case CastExpression a: Validate(a); break;
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
                case BinaryExpression bin: Validate(bin); break;
                case ObjectExpression obj: Validate(obj); break;
                case ParentesesExpression p: Validate(p); break;
                case TypeExpression t: Validate(t); break;
                case ContentExpression exp: ValidateExpression(exp); break;
            }
        }
        void Validate(Iterator it) {
            if (it.Validated) return;
            it.Validated = true;
            Validate(it.Left, false);
            Validate(it.Right);
            if (it.Parent is For f) {
                if (it.Left != null && it.Left.Type == null && it.Left is IdentifierExpression id) {
                    f.Start = new Var() { Token = id.Token };
                    f.Start.SetParent(f);
                    if (it.Right is RangeExpression range) {
                        (f.Start as Var).Initializer = range.Left;
                        f.Condition = new BinaryExpression(f) { Token = new Token { Type = TokenType.LOWER, Value = "<" }, Left = id, Right = range.Right };
                    }
                }
            }
        }
        void Validate(Function func) {
            if (func.Validated) return;
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
            b.Type = b.Owner.Base;
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
                }
                if (found == null) continue;
                if (found.IsNumber == false && found != Builder.String) {
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
            @enum.Base = cls;
            foreach (EnumMember child in @enum.Children) {
                child.Type = @enum.Base;
            }
            @enum.Validated = cls != null;
        }
        void Validate(For f) {
            if (f.Validated) return;
            switch (f.Start) {
                case Expression exp: Validate(exp); break;
                case Var v: Validate(v); break;
            }
            if (f.Condition != null) Validate(f.Condition);
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
                    if (item.Parent.FindParent<Class>() == null) {
                        Builder.Program.AddError(item.Token, Error.UnknownName);
                        return;
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
        //void Validate(IdentifierExpression id) {
        //    if (id.Token.Value == "this" && id.Parent.FindParent<Class>() is Class cls) {
        //        id.Type = cls;
        //    } else if (id.Parent.FindName(id.Token.Value) is ValueType vt) {
        //        id.From = vt;
        //        id.Type = vt.Type;
        //    } else {
        //        Builder.Program.AddError(id.Token, Error.UnknownName);
        //    }
        //    Validate(id.Type);
        //}
        void Validate(Return ret) {
            if (ret.Validated) return;
            ret.Validated = true;
            if (ret.Expression == null) {
                return;
            }
            Validate(ret.Expression);
        }
        void Validate(Class cls) {
            if (cls == null /*|| cls.HasGenerics*/) return;
            if (cls.IsEnum) return;
            if (cls.Validated) return;
            if (cls.IsNative == false) {
                Builder.SetRealName(cls);
            }
            if (cls.BaseToken != null) {
                if (Builder.Classes.TryGetValue(cls.BaseToken.Token.Value, out Class based) == false) {
                    Builder.Program.AddError(cls.BaseToken.Token, Error.UnknownType);
                    return;
                }
                cls.Base = based;
                Validate(cls.Base);
            }
            Validate(cls as Block);
        }
        void Validate(Var var) {
            if (var.Validated) return;
            var.Validated = true;
            if (var.Initializer != null) {
                if (var.Initializer is Expression p) {
                    Validate(p);
                } else {
                    Validate(var.Initializer);
                }
                if (var.Type == null) {
                    var.Type = var.Initializer.Type;
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
                    if (func != null && func.Access == AccessType.INSTANCE) {
                        var.Program.AddError(var.Token, Error.NameAlreadyExists);
                        return;
                    }
                }
            }
            if (var.Type == null || var.Type.Token == null) {
                Builder.Program.AddError(var.Token, Error.UndefinedType);
                return;
            }
            if (Builder.Find(var.Type.Token.Value/*, out AST from*/) is Class cls) {
                var.Type = cls;
                Validate(cls);
                return;
            }
            Builder.Program.AddError(var.Type.Token, Error.UnknownType);
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
                case TokenType.REAL:
                    literal.Type = Builder.F64;
                    break;
                case TokenType.NUMBER:
                    literal.Type = Builder.I32;
                    break;
                case TokenType.QUOTE:
                    literal.Type = Builder.String;
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
                //if (v.Type.IsTemporary) {
                //    if(Builder.Find(v.Type.Token.Value, out var from) is Class c) {
                //        v.Type = c;
                //    }
                //}
                Validate(v.Type);
                id.From = v;
                id.Type = v.Type;
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
            if (id.Parent is DotExpression dot) {
                var dotType = dot.Type;
                if (dot.Left == id && dot.Parent is DotExpression dp) {
                    dotType = dp.Left.Type;
                }
                if (dotType != null) {
                    if (ValidateClassMember(id, dotType)) {
                        return;
                    }
                    if (ValidateEnum(id, dot.Left.Token.Value)) {
                        return;
                    }
                }
            } else if (id.Parent is BinaryExpression bin && bin.Left == id && bin.Right is CallExpression) {

            }
            if (id.FindParent<Class>() is Class cls) {
                if (ValidateClassMember(id, cls)) {
                    return;
                }
            }
            if (id.Parent.FindName(id.Token.Value) is ValueType vt) {
                Validate(vt);
                id.From = vt;
                id.Type = vt.Type;
                Validate(id.Type);
                return;
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
            dot.Type = dot.Right.Type;
        }
        void Validate(BinaryExpression bin) {
            if (bin.Validated) return;
            bin.Validated = true;
            Validate(bin.Left);
            Validate(bin.Right);
            if (AreCompatible(Builder, bin.Left, bin.Right) == false) {
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
                return FindInClass(initial, cls.Base);
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
                return FindInClass(call, cls.Base);
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
                            var param = func.Parameters.Children[a] as Parameter;
                            if (AreCompatible(arg, param) == false) {
                                goto next;
                            }
                        }
                        call.Function = func;
                        call.Type = func.Type;
                        Validate(call.Type);
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
            r.Type = r.Content.Type;
            r.Type = Builder.Pointer;
        }
        void Validate(NewExpression n) {
            if (n.Validated) return;
            n.Validated = true;
            if (Builder.Classes.TryGetValue(n.QualifiedName, out Class cls) == false) {
                Builder.Program.AddError(n.Token, Error.UnknownType);
                return;
            }
            n.Type = cls;
            Validate(n.Type);
            if (n.Content is ArrayCreationExpression ind) {
                Validate(ind);
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
        void Validate(CastExpression a) {
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
            //if (index.Left.Type.ArrayOf == null) {
            //    index.Program.AddError(index.Left.Token, Error.IncompatibleType);
            //    return;
            //}
            Validate(index.Right);
            index.Type = index.Left.Type.ArrayOf?.Type ?? index.Left.Type;
            AddErrorIfNull(index);
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
            array.Type = array.Content.Type;
            AddErrorIfNull(array);
        }
        //void Validate(DeclaredType declared) {
        //    if (Builder.Classes.TryGetValue(declared.Token.Value, out Class cls) == false) {
        //        if (Builder.Enums.TryGetValue(declared.Token.Value, out Enum e) == false) {
        //            Builder.Program.AddError(declared.Token, Error.UnknownType);
        //            return;
        //        }
        //        cls = e.Type;
        //    }
        //    declared.Type = cls;
        //    if (declared.Caller != null) {
        //        //Validate(declared.Caller, exp);
        //    }
        //}
        void Validate(TypeOf type) {
            if (type.Validated) return;
            type.Validated = true;
            if (type.Content is IdentifierExpression id) {
                if (Builder.Classes.TryGetValue(id.Token.Value, out Class cls)) {
                    Validate(cls);
                    type.Type = id.Type = cls;
                    goto done;
                }
            }
            Validate(type.Content);
        done:
            if (Builder.Classes.TryGetValue("ReflectionType", out Class found) == false) {
                //type.Type = Builder.Classes["ReflectionType"];
                type.Type = found;
            }
        }
        //void Validate(Delete del) {
        //    for (int i = 0; i < del.Block.Children.Count; i++) {
        //        Validate(del.Block.Children[i]);
        //    }
        //}
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
            if (AreCompatible(Builder, ternary.False, ternary.True) == false) {
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
            foreach (ValueType param in call.Arguments) {
                //if (param is Null n) {
                //    param.Type = Builder.Pointer;
                //} else 
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
        public static bool AreCompatible(ValueType vt1, ValueType vt2) => AreCompatible(null, vt1, vt2);
        public static bool AreCompatible(Builder builder, ValueType vt1, ValueType vt2) {
            var t1 = vt1?.Type ?? null;
            var t2 = vt2?.Type ?? null;
            if (t1 == t2) return true;
            if (t1 == null || t2 == null) return false;
            if (t1?.ArrayOf != null && vt2 is IndexerExpression && t1.ArrayOf.Type == t2) return true;
            if (t2?.ArrayOf != null && vt1 is IndexerExpression && t2.ArrayOf.Type == t1) return true;
            if (t1 is Null && t2 is Null) return true;
            if (t1 is Null && (t2.IsPrimitive == false)) return true;
            if (t2 is Null && (t1.IsPrimitive == false)) return true;
            if (t1.IsNumber && t2.IsNumber) {
                return true;
            }
            if (t1.IsNative == t2.IsNative) {
                return !(builder != null && (t1 == builder.String || t2 == builder.String));
            }
            if (t1.Token.Value == t2.Token.Value) {
                return true;
            }
            return false;
        }
        public static bool AreCompatible(Class t1, Class t2) {
            if (t1 is null && t2 != null && (t2.IsPrimitive == false)) return true;
            if (t2 is null && t1 != null && (t1.IsPrimitive == false)) return true;
            if (t1 == null || t2 == null) return false;
            if (t1 == t2) return true;
            if (t1.IsAny || t2.IsAny) return true;
            if (t1.IsNumber && t2.IsNumber) return true;
            if (t1.Token.Value == t2.Token.Value) return true;
            if (t1.IsNative == t2.IsNative) return true;
            return false;
        }
    }
}
