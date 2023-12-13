//using System.Collections.Generic;
//using System.Text;

//namespace Run {

//    public class Cloner {
//        Builder Builder;
//        public Cloner(Builder builder) {
//            Builder = builder;
//        }

//        public T Clone<T>(T source, string name, List<Generic> generics) where T : Class {
//            var clone = source.Clone();
//            clone.Token = source.Token.Clone();
//            clone.Token.Value = name;
//            clone.Real = name;
//            clone.Base = source;
//            Builder.Classes.Add(name, clone);
//            clone.Generics = new List<Generic>(0);
//            return Clone(clone, source.Generics, generics, clone);
//        }

//        public Class Clone(Class cls, List<Generic> generics) {
//            if (cls.HasGenerics == false) return cls;
//            var clone = (cls.Parent as Block).Add<Class>();
//            clone.Token = cls.Token.Clone();
//            clone.Real = clone.Token.Value;
//            for (int i = 0; i < cls.Generics.Count; i++) {
//                var generic = cls.Generics[i];
//                if (generics[i].Token.Value == generic.Token.Value) {
//                }
//            }
//            return cls;
//        }

//        T Clone<T>(T ast, List<Generic> originals, List<Generic> generics, AST newParent) where T : AST {
//            switch (ast) {
//                case ExpressionV2 expression: return Clone(expression, originals, generics, newParent) as T;
//                case Function function: return Clone(function, originals, generics, newParent) as T;
//                case For f: return Clone(f, originals, generics, newParent) as T;
//                case If i: return Clone(i, originals, generics, newParent) as T;
//                case Switch sw: return Clone(sw, originals, generics, newParent) as T;
//                case Case cs: return Clone(cs, originals, generics, newParent) as T;
//                case Block block: return Clone(block, originals, generics, newParent) as T;
//                case Var v: return Clone(v, originals, generics, newParent) as T;
//                case Return ret: return Clone(ret, originals, generics, newParent) as T;
//                case Binary bin: return Clone(bin, originals, generics, newParent) as T;
//                case Unary un: return Clone(un, originals, generics, newParent) as T;
//                case New n: return Clone(n, originals, generics, newParent) as T;
//                case MemberAccess ma: return Clone(ma, originals, generics, newParent) as T;
//                case Identifier identifier: return Clone(identifier, originals, generics, newParent) as T;
//                case Array array: return Clone(array, originals, generics, newParent) as T;
//                case Caller caller: return Clone(caller, originals, generics, newParent) as T;
//                case Parenteses parenteses: return Clone(parenteses, originals, generics, newParent) as T;
//                case This t: return Clone(t, originals, generics, newParent) as T;
//            }
//            return ast;
//        }

//        This Clone(This t, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = t.Clone();
//            clone.Type = SetType(clone.Type, originals, generics);
//            return clone;
//        }

//        Parenteses Clone(Parenteses parenteses, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = parenteses.Clone();
//            clone.ExpressionV2 = Clone(clone.ExpressionV2, originals, generics, newParent);
//            return clone;
//        }

//        Caller Clone(Caller caller, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = caller.Clone();
//            clone.From = Clone(clone.From, originals, generics, newParent);
//            clone.Values = new List<AST>(caller.Values.Count);
//            foreach (AST exp in caller.Values) {
//                clone.Values.Add(Clone(exp, originals, generics, newParent));
//            }
//            return clone;
//        }

//        Array Clone(Array array, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = array.Clone();
//            clone.From = Clone(clone.From, originals, generics, newParent);
//            clone.Values = new List<AST>(array.Values.Count);
//            foreach (AST exp in array.Values) {
//                clone.Values.Add(Clone(exp, originals, generics, newParent));
//            }
//            return clone;
//        }

//        Identifier Clone(Identifier identifier, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = identifier.Clone();
//            clone.Type = SetType(clone.Type, originals, generics);
//            return clone;
//        }

//        MemberAccess Clone(MemberAccess ma, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = ma.Clone();
//            clone.This = Clone(clone.This, originals, generics, newParent);
//            clone.Member = Clone(clone.Member, originals, generics, newParent);
//            clone.Type = SetType(clone.Type, originals, generics);
//            return clone;
//        }

//        New Clone(New n, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = n.Clone();
//            clone.Type = SetType(clone.Type, originals, generics);
//            clone.Declared = Clone(clone.Declared, originals, generics, newParent);
//            return clone;
//        }

//        Unary Clone(Unary un, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = un.Clone();
//            clone.Right = Clone(clone.Right, originals, generics, newParent);
//            return clone;
//        }

//        Binary Clone(Binary bin, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = bin.Clone();
//            clone.Left = Clone(clone.Left, originals, generics, newParent);
//            clone.Right = Clone(clone.Right, originals, generics, newParent);
//            clone.Type = SetType(clone.Type, originals, generics);
//            return clone;
//        }

//        Case Clone(Case cs, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = cs.Clone();
//            clone.Expressions = new List<ExpressionV2>(cs.Expressions.Count);
//            foreach (ExpressionV2 exp in cs.Expressions) {
//                clone.Expressions.Add(Clone(exp, originals, generics, newParent));
//            }
//            return clone;
//        }

//        Switch Clone(Switch sw, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = sw.Clone();
//            return clone;
//        }

//        Return Clone(Return ret, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = ret.Clone();
//            return clone;
//        }

//        Block Clone(Block block, List<Generic> originals, List<Generic> generics, AST newParent) {
//            if (block.Children == null) return block;
//            var children = block.Children;
//            block.Children = new List<AST>(block.Children.Count);
//            foreach (AST child in children) {
//                block.Add(Clone(child, originals, generics, newParent));
//            }
//            return block;
//        }
//        ExpressionV2 Clone(ExpressionV2 expression, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = expression.Clone();
//            clone.Result = Clone(expression.Result, originals, generics, newParent);
//            return clone;
//        }

//        Function Clone(Function function, List<Generic> originals, List<Generic> generics, AST newParent) {
//            if (function.HasGenerics == false) return function;
//            var clone = function.Clone();
//            var type = clone.Type;
//            if (clone is Constructor && newParent is Class cls) {
//                clone.Type = cls;
//            } else {
//                clone.Type = SetType(clone.Type, originals, generics);
//            }
//            clone.Parameters = new Block();
//            if (function.Parameters != null) {
//                foreach (Parameter p in function.Parameters.Children) {
//                    var param = p.Clone();
//                    //var pType = param.Type;
//                    param.Type = SetType(param.Type, originals, generics);
//                    //if (param.Type != pType) param.HasGenerics = false;
//                    clone.Parameters.Add(param);
//                }
//            }
//            if (clone.Parameters.Children.Count == 0) {
//                Clone(clone as Block, originals, generics, newParent);
//            } else {
//                Clone(clone.Children[1], originals, generics, newParent);
//            }
//            if (clone.Type != type) {
//                clone.Generics = null;
//                var buff = new StringBuilder(clone.Type.Token.Value).Append('_').Append(function.Token.Value);
//                Builder.SetRealName(clone, buff);
//                clone.Real = buff.ToString();
//                Builder.Functions.Add(clone.Real, clone);
//            }
//            return clone;
//        }

//        Class SetType(Class type, List<Generic> originals, List<Generic> generics) {
//            if (type != null && type.IsTemporary) {
//                for (int i = 0; i < originals.Count; i++) {
//                    var original = originals[i];
//                    if (original.Token.Value == type.Token.Value) {
//                        return Builder.Classes[generics[i].Token.Value];
//                    }
//                }
//            }
//            return type;
//        }

//        Var Clone(Var v, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = v.Clone();
//            var type = clone.Type;
//            clone.Type = SetType(clone.Type, originals, generics);
//            if (clone.Type != type) {
//                clone.Generics = null;
//            }
//            return clone;
//        }
//        For Clone(For f, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = f.Clone();
//            return clone;
//        }
//        If Clone(If i, List<Generic> originals, List<Generic> generics, AST newParent) {
//            var clone = i.Clone();
//            return clone;
//        }

//        void SetFunctionGeneric(Class cls, Function func, List<Generic> generics) {
//            foreach (Var v in func.Find<Var>()) {
//                SetVarGeneric(cls, v, generics);
//            }
//        }

//        void SetVarGeneric(Class cls, Var v, List<Generic> generics) {
//            if (v.HasGenerics == false) return;
//            v.Generics = null;
//            for (int i = 0; i < cls.Generics.Count; i++) {
//                var generic = cls.Generics[i];
//                if (generic.Token.Value == v.Type.Token.Value) {
//                    v.Type = Builder.Classes[generics[i].Token.Value];
//                    return;
//                }
//            }
//        }
//    }
//}
