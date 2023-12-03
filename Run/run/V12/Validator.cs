using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Run.V12 {
    public class Validator {
        public Builder Builder;
        public Validator(Builder builder) {
            Builder = builder;
        }
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
            if (block.Validated) {
                return;
            }
            block.Validated = true;
            switch (block) {
                case Delete d:
                    Validate(d);
                    return;
            }
            for (int i = 0; i < block.Children.Count; i++) {
                var item = block.Children[i];
                switch (item) {
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
                    case Expression exp: Validate(exp); break;
                    case Delete del: Validate(del); break;
                    case Block b: Validate(b); break;
                    case Return ret: Validate(ret); break;
                    case TypeOf tp: Validate(tp); break;
                    default: break;
                }
            }
            if (block is Function func && func.IsArrow && func.Children.Count > 0) {
                Expression e = func.Children.Find(c => c is Expression) as Expression;
                Validate(func.Type);
                if (func.Type != null && AreCompatible(e.Type, func.Type) == false) {
                    Builder.Program.AddError(func.Token, Error.IncompatibleType);
                }
            }
        }

        void Validate(Label label) {
            var func = label.FindParent<Function>();
            foreach (var lbl in func.Find<Label>()) {
                if (lbl == label) continue;
                if (lbl.Token.Value == label.Token.Value) {
                    Builder.Program.AddError(Error.NameAlreadyExists, label);
                    return;
                }
            }
        }

        void Validate(Goto gt) {
            var func = gt.FindParent<Function>();
            foreach (var lbl in func.Find<Label>()) {
                if (lbl.Token.Value == gt.Token.Value) {
                    return;
                }
            }
            Builder.Program.AddError(Error.UnknownName, gt);
        }

        void Validate(Base b) {
            if (b.Owner == null) {
                return;
            }
            b.Type = b.Owner.Base;
            Validate(b.Type);
        }

        void Validate(TypeOf tp) {
            Validate(tp.Expression);
            tp.Type = tp.Expression.Type;
            Validate(tp.Type);
        }

        void Validate(Property property) {
            if (Builder.Classes.TryGetValue(property.Type.Token.Value, out Class type) == false) {
                Builder.Program.AddError(property.Type.Token, Error.UnknownType);
                return;
            }
            property.Type = type;
            Validate(type);
            if (property.Getter != null) {
                property.Getter.Type = type;
                Validate(property.Getter);
            }
            if (property.Setter != null) {
                Validate(property.Setter);
            }
            if (property.Initializer != null) {
                Validate(property.Initializer);
            }
        }

        void Validate(Switch sw) {
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
            Class cls = null, found = null;
            foreach (EnumMember child in @enum.Children) {
                if (child.Expression != null) {
                    Validate(child.Expression);
                    found = child.Expression.Type;
                } else {
                    found = Builder.I32;
                }
                if (found == null) continue;
                if (found != Builder.I32 && found != Builder.String) {
                    Builder.Program.AddError(Error.IncompatibleType, child);
                    continue;
                }
                if (cls == null) {
                    cls = found;
                } else if (cls != found) {
                    Builder.Program.AddError(Error.IncompatibleType, child);
                }
                //child.Type = cls;
            }
            Validate(cls);
            @enum.Type.Real = cls.Real;
            @enum.Type.IsPrimitive = cls.IsPrimitive;
            foreach (EnumMember child in @enum.Children) {
                child.Type = @enum.Type;
            }
            //@enum.Type = cls;
        }

        void Validate(For f) {
            switch (f.Start) {
                case Expression exp: Validate(exp); break;
                case Var v: Validate(v); break;
            }
            if (f.Condition != null) Validate(f.Condition);
            if (f.Step != null) Validate(f.Step);
            Validate(f as Block);
        }

        void Validate(Else e) {
            if (e.Condition != null) Validate(e.Condition);
            Validate(e as Block);
        }

        void Validate(If If) {
            Validate(If.Condition);
            Validate(If as Block);
        }
        void Validate(Delete delete) {
            foreach (var item in delete.Children) {
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
                    case Identifier i: Validate(i); break;
                    default:
                        Builder.Program.AddError(Error.UnknownType, item); break;
                }
            }
        }

        void Validate(Identifier id) {
            if (id.Token.Value == "this" && id.Parent.FindParent<Class>() is Class cls) {
                id.Type = cls;
            } else if (id.Parent.FindName(id.Token.Value) is ValueType vt) {
                id.From = vt;
                id.Type = vt.Type;
            } else {
                Builder.Program.AddError(Error.UnknownName, id);
            }
            Validate(id.Type);
        }
        void Validate(Return ret) {
            if (ret.Expression == null) {
                return;
            }
            Validate(ret.Expression);
        }
        void Validate(Class cls) {
            if (cls == null /*|| cls.HasGenerics*/) return;
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
            if (var.Initializer != null) {
                Validate(var.Initializer);
                if (var.Type == null) {
                    var.Type = var.Initializer.Type;
                    return;
                }
            }
            var func = var.FindParent<Function>();
            //if (func != null && func.FindChildren(var.Token.Value) is AST ast && ast != var) {
            //    if (var.HasGenerics == false && ast.HasGenerics == false) {
            //        var.Program.AddError(var.Token, Error.NameAlreadyExists);
            //        return;
            //    }
            //}
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
            if (Builder.Find(var.Type.Token.Value, out AST from) is Class cls) {
                var.Type = cls;
                Validate(cls);
                return;
            }
            //if (var.HasGenerics == false)
            Builder.Program.AddError(var.Type.Token, Error.UnknownType);
        }
        public void Validate(Expression exp) {
            Validate(exp.Result, exp);
            exp.Type = (exp.Result as ValueType)?.Type;
            Validate(exp.Type);
        }
        void Validate(Literal literal, Expression exp) {
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
                    literal.Type = Builder.Byte;
                    break;
                default:
                    exp.Program.AddError(literal.Token, Error.UnknownType);
                    return;
            }
            Validate(literal.Type);
        }
        void Validate(Identifier id, Expression exp) {
            if (id.Type != null && id.Type.IsTemporary == false) return;
            if (id.Parent is MemberAccess acess && acess.Type != null) {
                if (acess.Type.IsTemporary && Builder.Classes.TryGetValue(acess.Type.Token.Value, out Class c)) {
                    acess.Type = c;
                    Validate(c);
                }
                if (acess.This is Identifier thisID && thisID.From is Enum en) {
                    foreach (EnumMember bin in en.Children) {
                        if (bin.Token.Value == id.Token.Value) {
                            id.From = bin;
                            id.Type = en.Type;
                            Validate(id.Type);
                            return;
                        }
                    }
                }
                if (acess.Type.FindMember<Var>(id.Token.Value) is ValueType ast) {
                    id.From = ast;
                    id.Type = ast.Type;
                    Validate(id.Type);
                    return;
                }
                if (acess.Member == id) {
                    goto error;
                }
            }
            var cls = exp.FindParent<Class>();
            if (cls != null && cls.FindMember<Var>(id.Token.Value) is Var v) {
                id.From = v;
                id.Type = v.Type;
                Validate(id.Type);
                return;
            }
            if (id.Parent.FindName(id.Token.Value) is ValueType vt) {
                id.From = vt;
                id.Type = vt.Type;
                Validate(id.Type);
                return;
            }
            if (Builder.Find(id.Token.Value, out var from) is Class c1) {
                id.From = from;
                id.Type = c1;
                Validate(id.Type);
                return;
            }
        error:
            Builder.Program.AddError(Error.UnknownName, id);
        }
        void Validate(MemberAccess dot, Expression exp) {
            dot.Type = (dot.Parent as ValueType)?.Type;
            Validate(dot.Type);
            Validate(dot.This, exp);
            dot.Type = (dot.This as ValueType).Type;
            Validate(dot.Type);
            Validate(dot.Member, exp);
            if (dot.Member is ValueType vt && vt.Type != null) {
                dot.Type = vt.Type;
                Validate(dot.Type);
                return;
            }
            if (dot.Type == null)
                dot.Program.AddError(dot.Member.Token, Error.UnknownClassMember);
        }
        void Validate(Array array, Expression exp) {
            if (array.Token.Value == "this") {
                if (exp.FindParent<Class>() is Class cls) {
                    array.Type = cls;
                } else {
                    Builder.Program.AddError(Error.UnknownType, array);
                    return;
                }
            } else if (exp.FindName(array.Token.Value) is ValueType vt) {
                array.Type = vt.Type;
                array.From = vt;
            } else if (Builder.Classes.TryGetValue(array.Token.Value, out Class cls)) {
                array.Type = cls;
            } else if (array.FindName(array.Token.Value) is ValueType vt1) {
                array.Type = vt1.Type;
                array.From = vt1;
            } else {
                Builder.Program.AddError(Error.UnknownType, array);
                return;
            }
            if (array.Type == null) {
                Builder.Program.AddError(Error.UnknownType, array);
                return;
            }
            if (array.Type.ArrayOf != null) {
                array.Type = array.Type.ArrayOf.Type;
            }
            Validate(array.Type);
            ValidateParameters(array, exp);
        }

        string GetRealName(Caller call, Expression exp) {
            StringBuilder buff = new StringBuilder(call.Token.Value);
            if (call.Parent != null && call.Parent.Parent is New) {
                buff.Append("_this");
            }
            foreach (ValueType param in call.Values) {
                if (param is Null n) {
                    param.Type = Builder.Pointer;
                } else if (param.Type == null) {
                    Validate(param, exp);
                }
                if (param.Type == null) {
                    param.Program.AddError(param.Token, Error.UnknownType);
                    return null;
                }
                buff.Append('_').Append(param.Type.Token.Value);
            }
            return buff.ToString();
        }

        AST FindFunctionInMemberAccess(MemberAccess dot, string name) {
            if (dot.Type != null && FindFunctionInClass(dot.Type, name) is Function f) return f;
            if (dot.This is ValueType vt && vt.Type is Class cls) {
                return FindFunctionInClass(cls, name);
            }
            return null;
        }

        Function FindFunctionInClass(Class cls, string name) {
            if (GetFunction(cls.Token.Value + "_" + name) is Function f1) return f1;
            if (cls.FindMember<Function>(name) is Function f) return f;
            return null;
        }

        IEnumerable<Function> FindClosestFunction(Block block, string name, int argsCount) {
            for (int i = 0; i < block.Children.Count; i++) {
                var func = block.Children[i] as Function;
                if (func == null) continue;
                if ((func.Token.Value == name || func.Real == name) && (func.Parameters == null ? argsCount == 0 : (func.Parameters.Children.Count == argsCount))) {
                    yield return func;
                }
            }
            //for (int i = 0; i < block.Children.Count; i++) {
            //    var func = block.Children[i] as Function;
            //    if (func == null) continue;
            //    if ((func.Real.Contains(name)) && (func.Parameters == null ? argsCount == 0 : (func.Parameters.Children.Count == argsCount))) {
            //        yield return func;
            //    }
            //}
            if (block is Class cls && cls.IsBased) {
                foreach (var func in FindClosestFunction(cls.Base, name, argsCount)) {
                    yield return func;
                }
            }
        }

        bool ValidateCall(Caller call, Function func) {
            if (call.Values.Count > 0) {
                for (int i = 0; i < call.Values.Count; i++) {
                    var value = call.Values[i] as ValueType;
                    var param = func.Parameters.Children[i] as Parameter;
                    if (value.Type == null) return false;
                    if (value.Type.IsCompatible(param.Type) == false) {
                        return false;
                    }
                }
            }
            return true;
        }

        Function FindFunction(Caller call) {
            var real = GetRealName(call, call.FindParent<Expression>());
            if (GetFunction(real) is Function f) {
                return f;
            }
            if (call.Parent is MemberAccess dot && dot.Type != null) {
                foreach (var func in FindClosestFunction(dot.Type, call.Token.Value, call.Values.Count)) {
                    if (ValidateCall(call, func)) return func;
                }
            }
            if (call.FindParent<Class>() is Class cls) {
                foreach (var func in FindClosestFunction(cls, call.Token.Value, call.Values.Count)) {
                    if (ValidateCall(call, func)) return func;
                }
            }
            if (call.FindParent<New>() is New) {
                if (Builder.Classes.TryGetValue(call.Token.Value, out Class newCls)) {
                    foreach (var func in FindClosestFunction(newCls, call.Token.Value, call.Values.Count)) {
                        if (ValidateCall(call, func)) return func;
                    }
                }
            }
            if (call.FindParent<Module>() is Module module) {
                foreach (var func in FindClosestFunction(module, call.Token.Value, call.Values.Count)) {
                    if (ValidateCall(call, func)) return func;
                }
            }
            return null;
        }

        void Validate(Caller call, Expression exp, bool addError = true) {
            AST found = FindFunction(call);
            switch (found) {
                case Function f:
                    call.Type = f.Type;
                    call.Real = f.Real;
                    call.Function = f;
                    break;
                case Var v:
                    call.Type = v.Type;
                    call.Real = v.Type.Token.Value;
                    break;
                case Class cls:
                    call.Type = cls;
                    if (cls.IsNative) {
                        call.Real = cls.Real;
                    } else {
                        call.Real = cls.Token.Value;
                    }
                    break;
                default:
                    if (addError) {
                        Builder.Program.AddError(call.Token, Error.UnknownFunctionNameOrWrongParamaters);
                    }
                    return;
            }
            Validate(call.Type);
            ValidateParameters(call, exp);
        }

        Function GetFunction(string name) {
            if (name == null) return null;
            if (Builder.Functions.TryGetValue(name, out Function found)) {
                return found;
            }
            foreach (var f in Builder.Functions.Values) {
                if (f.HasVariadic) {
                    var vname = f.Real.Substring(0, f.Real.Length - 9);
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
        void ValidateParameters(Caller caller, Expression exp) {
            if (caller.Values == null || caller.Values.Count == 0) return;
            for (int i = 0; i < caller.Values.Count; i++) {
                Validate(caller.Values[i], exp);
            }
        }

        void Validate(Scope scope, Expression exp) {
            if (Builder.Classes.TryGetValue(scope.Token.Value, out Class cls) == false) {
                Builder.Program.AddError(scope.Token, Error.UnknownType);
                return;
            }
            var ctors = cls.Find<Constructor>().ToList();
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

        void Validate(SizeOf sizeOf, Expression exp) {
            Validate(sizeOf.Expression);
            sizeOf.Type = Builder.I32;
            Validate(sizeOf.Type);
        }

        void Validate(Ref r, Expression exp) {
            Validate(r.Expression.Result, exp);
            r.Expression.Type = (r.Expression.Result as ValueType).Type;
            r.Type = Builder.Pointer;
        }

        void Validate(New n, Expression exp) {
            if (n.Expression.Result is Caller call && call is not Array) {
                //Validate(call, exp, false);
                //if (call.Type == null) {
                //    if (Builder.Find(call.Token.Value) is Class cls) {
                //        call.Type = n.Type = cls;
                //        return;
                //    }
                //}
            }
            Validate(n.Expression);
            n.Type = (n.Expression)?.Type;
        }
        void Validate(AST ast, Expression exp) {
            if (ast is ValueType vt && vt.Type != null) return;
            switch (ast) {
                case Delete del: Validate(del, exp); break;
                case Identifier id: Validate(id, exp); break;
                case Literal lit: Validate(lit, exp); break;
                case Array array: Validate(array, exp); break;
                case Caller call: Validate(call, exp); break;
                case Ternary ter: Validate(ter, exp); break;
                case Parenteses p: Validate(p, exp); break;
                case Base b: Validate(b); break;
                case MemberAccess dot: Validate(dot, exp); break;
                case Cast cast: Validate(cast, exp); break;
                case Scope s: Validate(s, exp); break;
                case SizeOf s: Validate(s, exp); break;
                case As a: Validate(a, exp); break;
                case Ref r: Validate(r, exp); break;
                case TypeOf t: Validate(t, exp); break;
                case New n: Validate(n, exp); break;
                case Comparation comp: Validate(comp, exp); break;
                case Binary bin: Validate(bin, exp); break;
                case This t: Validate(t, exp); break;
                case Unary un: Validate(un, exp); break;
                case DeclaredType dc: Validate(dc, exp); break;
                default: break;
            }
            if (ast is ValueType v && v.Type != null && v.Type.Validated == false) {
                Validate(v.Type);
            }
        }

        void Validate(DeclaredType declared, Expression exp) {
            if (Builder.Classes.TryGetValue(declared.Token.Value, out Class cls) == false) {
                if (Builder.Enums.TryGetValue(declared.Token.Value, out Enum e) == false) {
                    Builder.Program.AddError(declared.Token, Error.UnknownType);
                    return;
                }
                cls = e.Type;
            }
            declared.Type = cls;
            if (declared.Caller != null) {
                Validate(declared.Caller, exp);
            }
        }

        void Validate(TypeOf type, Expression exp) {
            if (type.Expression.Result is Identifier id) {
                if (Builder.Classes.TryGetValue(id.Token.Value, out Class cls)) {
                    Validate(cls);
                    type.Expression.Type = id.Type = cls;
                    goto done;
                }
            }
            Validate(type.Expression);
        done:
            type.Type = Builder.Classes["ReflectionType"];
        }

        void Validate(As a, Expression exp) {
            Validate(a.Left, exp);
            Validate(a.Declared, exp);
            a.Type = a.Declared.Type;
            //TODO
            //switch (a.Right) {
            //    case Identifier id:
            //        if (Builder.Classes.TryGetValue(id.Token.Value, out Class cls)) {
            //            a.Type = cls;
            //            return;
            //        }
            //        if (Builder.Enums.TryGetValue(id.Token.Value, out Enum e)) {
            //            a.Type = e.Type;
            //            return;
            //        }
            //        Builder.Program.AddError(id.Token, Error.UnknownType);
            //        break;
            //    case Array array:
            //        if (Builder.Classes.TryGetValue(array.Token.Value, out Class cl) == false) {
            //            Builder.Program.AddError(array.Token, Error.UnknownType);
            //            return;
            //        }
            //        if (array.Values.Count > 0) {
            //            Builder.Program.AddError(Error.InvalidExpression, a);
            //            return;
            //        }
            //        a.Type = array.Type = cl;
            //        break;
            //    default:
            //        Builder.Program.AddError(Error.InvalidExpression, a);
            //        break;
            //}
        }

        void Validate(Delete del, Expression exp) {
            for (int i = 0; i < del.Children.Count; i++) {
                Validate(del.Children[i], exp);
            }
        }

        void Validate(Comparation comp, Expression exp) {
            Validate(comp as Binary, exp);
            comp.Type = Builder.Bool;
        }

        void Validate(Ternary ternary, Expression exp) {
            Validate(ternary.Condition, exp);
            switch (ternary.Condition) {
                case Comparation: break;
                case This: break;
                case Identifier id when id.Type != null && id.Type.Token.Value == "bool": break;
                case Literal lit when lit.Type != null && lit.Type.Token.Value == "bool": break;
                case Parenteses par when par.Type != null && par.Type.Token.Value == "bool": break;
                default:
                    if (ternary.Condition is ValueType vt && vt.Type != null) {
                        break;
                    } else {
                        exp.Program.AddError(ternary.Token, Error.InvalidExpression);
                        return;
                    }
            }
            Validate(ternary.IsTrue);
            Validate(ternary.IsFalse);
            if (AreCompatible(ternary.IsFalse, ternary.IsTrue) == false) {
                Builder.Program.AddError(Error.IncompatibleType, ternary.Condition);
                return;
            }
            ternary.Type = ternary.IsTrue.Type;
        }

        void Validate(Unary unary, Expression exp) {
            if (unary.Right != null) {
                Validate(unary.Right, exp);
            }
            if (unary.Right is ValueType vt && vt.Type != null && vt.Type.IsNumber) {
                unary.Type = vt.Type;
                return;
            }
            Builder.Program.AddError(Error.InvalidExpression, unary.Right);
        }

        void Validate(This t, Expression exp) {
            if (t.Type == null) {

            }
        }

        void Validate(Parenteses p, Expression exp) {
            Validate(p.Expression, exp);
            p.Type = (p.Expression as ValueType)?.Type;
        }
        void Validate(Cast cast, Expression exp) {
            if (Builder.Classes.TryGetValue(cast.Token.Value, out Class cls) == false) {
                Builder.Program.AddError(cast.Token, Error.UndefinedType);
                return;
            }
            cast.Type = cls;
            Validate(cast.Expression);
        }
        void Validate(Binary bin, Expression exp) {
            var left = bin.Left as ValueType;
            var right = bin.Right as ValueType;
            if (left == null || right == null) {
                Builder.Program.AddError(Error.InvalidExpression, bin);
                return;
            }
            Validate(left, exp);
            Validate(right, exp);
            if (AreCompatible(left is Expression l ? l.Result as ValueType : left, right is Expression r ? r.Result as ValueType : right) == false) {
                Builder.Program.AddError(right.Token ?? left.Token, Error.IncompatibleType);
                return;
            }
            switch (bin.Token.Family) {
                case TokenType.LOGICAL:
                    bin.Type = Builder.Bool;
                    break;
                default:
                    bin.Type = right.Type;
                    break;
            }
        }

        public static bool AreCompatible(ValueType vt1, ValueType vt2) {
            var t1 = vt1.Type;
            var t2 = vt2.Type;
            if (vt1 is Null && vt2 is Null) return true;
            if (vt1 is Null && t2 != null && t2.IsPrimitive == false) return true;
            if (vt2 is Null && t1 != null && t1.IsPrimitive == false) return true;
            if (t1 == null || t2 == null) return false;
            if (t1 == t2) return true;
            if (t1.IsNumber && t2.IsNumber) return true;
            if (t1.IsNative == t2.IsNative) return true;
            if (t1.Token.Value == t2.Token.Value) return true;
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
