using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Run {
    public class Builder {
        public Dictionary<string, Class> Classes = new(0);
        public Class Bool;
        public Class Byte;
        public Class Char;
        public Class F64;
        public Class F32;
        public Class String;
        public Class CharSequence = new() {
            IsNative = true,
            NativeName = "char",
            Real = "char",
            Token = new Token { Value = "chars" },
        };
        public Class I32;
        public Class I8;
        public Class U32;
        public Class Pointer;
        public Class Array;
        public Null Null = new() {
            Token = new Token {
                Value = "NULL",
            },
        };
        public Class Any;
        //public Dictionary<string, Enum> Enums = new(0);
        public Dictionary<string, Function> Functions = new(0);
        public static Builder Instance { get; private set; }
        public Program Program;
        public Builder(Program program) {
            Program = program;
            Instance = this;
        }

        public void Build(bool includeBuiltin = true) {
            if (includeBuiltin) {
                RegisterBuiltinTypes();
            }
            if (Program.HasErrors || Program.Errors.Count > 0) {
                return;
            }
            RegisterClasses();
            CorrectVarTemporaryTypes();
            RegisterFunctions();
            ValidateInterfaces();
        }

        private void ValidateInterfaces() {
            foreach (var cls in Classes.Values) {
                ValidateInterfaces(cls);
            }
        }

        void RegisterBuiltinTypes() {
            //Program.Add<Using>().LoadModule("system");
            Program.Add<Using>().LoadModule("builtin");
        }

        public Class Find(string name) {
            if (Classes.TryGetValue(name, out Class cls)) {
                return cls;
            }
            return null;
        }

        void RegisterFunctions() {
            foreach (var extension in Program.FindChildren<Extension>()) {
                if (Classes.TryGetValue(extension.Token.Value, out Class cls) == false) {
                    Program.AddError(extension.Token, Error.UnknownType);
                    continue;
                }
                foreach (var child in extension.Children) {
                    switch (child) {
                        case Function func:
                            func.Parent = cls;
                            cls.Children.Add(func);
                            break;
                        case GetterSetter property:
                            property.Parent = cls;
                            cls.Children.Add(property);
                            break;
                        default:
                            Program.AddError(child.Token, Error.InsideExtensionScopeOnlyFunctionsAreAllowed);
                            break;
                    }
                }
                extension.Children.Clear();
            }
            foreach (var func in Program.FindChildren<Function>()) {
                RegisterFunction(func);
            }

            foreach (var property in Program.FindChildren<GetterSetter>()) {
                if (property.SimpleKind != PropertyKind.None) {
                    continue;
                }
                if (property.Setter != null) {
                    RegisterFunction(property.Setter);
                }
                if (property.Getter != null) {
                    RegisterFunction(property.Getter);
                }
            }
        }

        void RegisterFunction(Function func) {
            if (func.Type != null) {
                if (Classes.TryGetValue(func.Type.Token.Value, out Class cls) == false) {
                    Program.AddError(func.Type.Token, Error.UnknownType);
                    return;
                }
                func.Type = cls;
            }
            SetRealName(func);
            if (Functions.TryAdd(func.Real, func) == false) {
                Program.AddError(func.Token, Error.NameAlreadyExists);
            }
        }

        void RegisterClasses() {
            Classes.Add("any", Any = new Class {
                ID = Class.CounterID++,
                Token = new Token { Value = "any" },
                Real = "pointer",
                NativeName = "void*",
                IsPrimitive = true,
                IsNative = true,
                IsAny = true,
            });
            foreach (var cls in Program.FindChildren<Class>()) {
                if (Classes.TryAdd(cls.Token.Value, cls) == false) {
                    Program.AddError(cls.Token, Error.NameAlreadyExists);
                    continue;
                }
                cls.ID = Class.CounterID++;
                SetBuiltinTypes(cls);
                SetDefaultConstructor(cls);
            }
            foreach (var cls in Classes.Values.ToArray()) {
                ValidateBased(cls);
            }
        }

        void CorrectVarTemporaryTypes() {
            foreach (var var in Program.DeepFindChildrenInternal<Var>()) {
                if (var.Type != null && var.Type.IsTemporary) {
                    if (Classes.TryGetValue(var.Type.Token.Value, out Class cls)) {
                        var.Type = cls;
                    }
                }
            }
        }

        private void SetBuiltinTypes(Class cls) {
            switch (cls.Token.Value) {
                case "bool": Bool = cls; break;
                case "string": String = cls; break;
                case "chars": CharSequence = cls; break;
                case "i32": I32 = cls; break;
                case "u32": U32 = cls; break;
                case "byte": Byte = cls; break;
                case "char": Char = cls; break;
                case "i8": I8 = cls; break;
                case "f64": F64 = cls; break;
                case "f32": F32 = cls; break;
                case "pointer": Pointer = cls; break;
                case "array": Array = cls; break;
            }
        }

        private static void SetDefaultConstructor(Class cls) {
            if (cls.IsEnum == false && cls is not Interface && /*cls.IsNative == false && */cls.Children.Any(c => c is Constructor) == false) {
                var ctor = cls.Add<Constructor>();
                ctor.IsDefault = true;
                ctor.Type = cls;
                ctor.Real = cls.Token.Value + "_this";
                ctor.Token = new Token {
                    Value = "this",
                    Scanner = cls.Token.Scanner,
                    Position = cls.Token.Position,
                    Line = cls.Token.Line,
                };
            }
        }

        private bool ValidateBased(Class cls) {
            if (cls.IsBased == false || cls.Base != null) return true;
            if (Classes.TryGetValue(cls.BaseToken.Token.Value, out Class baseCls) == false) {
                Program.AddError(cls.BaseToken.Token, Error.UnknownType);
                return false;
            }
            cls.Base = baseCls;
            //cls.Base = ValidateGenerics(baseCls, cls.Based.Generics);
            //if (cls.Token.Value != cls.Based.Token.Value) {
            //    cls.Based.Token = cls.Base.Token;
            //}
            return true;
        }

        //private Class ValidateGenerics(Class cls, List<Generic> generics) {
        //    if (generics == null || generics.Count == 0 || cls.HasGenerics == false) return cls;
        //    for (int i = 0; i < generics.Count; i++) {
        //        var generic = generics[i];
        //        if (Classes.ContainsKey(generic.Token.Value) == false) {
        //            return cls;
        //        }
        //    }
        //    return CloneClass(cls, generics);
        //}

        //Class CloneClass(Class cls, List<Generic> generics) {
        //    if (cls == null || generics == null || generics.Count == 0) return cls;
        //    var name = cls.Token.Value + "_" + string.Join("_", generics.Select(g => g.Token.Value));
        //    if (Classes.TryGetValue(name, out Class other)) {
        //        return other;
        //    }
        //    return new Cloner(this).Clone(cls, name, generics);
        //}

        void ValidateInterfaces(Class cls) {
            if (cls.IsBased) {
                if (GetInterface(cls.BaseToken.Token, false) is Interface ifc) {
                    cls.Interfaces ??= new(1);
                    cls.Interfaces.Add(ifc);
                }
            }
            if (cls.HasInterfaces == false) return;
            for (int i = 0; i < cls.Interfaces.Count; i++) {
                var inter = cls.Interfaces[i];
                var iFace = GetInterface(inter.Token);
                if (iFace == null) continue;
                bool ok = false;
                foreach (var child in cls.Children) {
                    if (child is Function func) {
                        if (CheckInterface(iFace, func)) {
                            ok = true;
                            break;
                        }
                    }
                }
                if (ok) {
                    cls.Interfaces[i] = iFace;
                } else {
                    Program.AddError(inter.Token, Error.InterfaceNotImplementedCorrect);
                }
            }
        }

        bool CheckInterface(Interface face, Function func) {
            if (face.FindMember<Function>(func.Token.Value) is Function f) {
                if (f.Parameters?.Children?.Count != func.Parameters?.Children?.Count) {
                    Program.AddError(func.Token, Error.InterfaceMemberHasDifferentParameters, true);
                    return false;
                }
                if (f.Type == null && func.Type == null) {
                    return true;
                }
                if (f.Type.Token.Value != func.Type.Token.Value) {
                    Program.AddError(func.Token, Error.InterfaceMemberHasDifferentReturnType, true);
                    return false;
                }
                return true;
            }
            return false;
        }

        Interface GetInterface(Token token, bool addError = true) {
            if (Classes.TryGetValue(token.Value, out Class face) == false) {
                Program.AddError(token, Error.UnknownType);
                return null;
            }
            var iFace = face as Interface;
            if (iFace == null && addError) {
                Program.AddError(token, Error.UnknownName);
                return null;
            }
            return iFace;
        }

        public void SetRealName(AST ast) {
            if (string.IsNullOrEmpty(ast.Real) == false) return;
            var buff = new StringBuilder();
            if (ast is not Function && ast.FindParent<Function>() is Function f) {
                buff.Append(f.Token.Value).Append('_');
            } else if (ast is not Class && ast.FindParent<Class>() is Class cls) {
                buff.Append(cls.Token.Value).Append('_');
                //} else if (ast is not Module && ast.FindParent<Module>() is Module m && m != Program) {
                //    buff.Append(string.IsNullOrEmpty(m.Nick) ? m.Token.Value : m.Nick).Append('_');
                //} else if (ast is Function) {
                //    buff.Append("_");
            }
            if (ast is Operator op) {
                buff.Append("_operator_").Append(op.Token.Type.ToString());
            } else {
                buff.Append(ast.Token.Value);
            }
            SetRealName(ast, buff);
            ast.Real = buff.ToString();
        }

        public void SetRealName(AST ast, StringBuilder buff) {
            if (ast is Function func && func.Parameters != null) {
                var cls = func.FindParent<Class>();
                for (int i = 0; i < func.Parameters.Children.Count; i++) {
                    var p = func.Parameters.Children[i] as Parameter;
                    if (p.IsMember && (p.Type == null || p.Type.IsTemporary)) {
                        if (cls == null) {
                            Program.AddError(func.Token, Error.OnlyInClassScope);
                            return;
                        }
                        if (cls.FindMember<Var>(p.Token.Value) is Var v) {
                            p.Type = v.Type;
                            p.Arguments = v.Arguments;
                        } else {
                            Program.AddError(p.Token, Error.UnknownName);
                            return;
                        }
                    } else if (p.Constraints != null) {
                        for (int t = 0; t < p.Constraints.Count; t++) {
                            buff.Append('_').Append(p.Constraints[t].Token.Value);
                        }
                    }
                    if (p.IsVariadic) {
                        buff.Append("_variadic");
                    } else {
                        if (p.Type != null) {
                            buff.Append('_').Append(p.Type.Token.Value);
                        }
                    }
                }
            }
        }
    }
}