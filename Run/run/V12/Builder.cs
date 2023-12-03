using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Run.V12 {
    public class Builder {
        public Dictionary<string, Class> Classes = new(0);
        public Class Bool;
        public Class Byte;
        public Class Char;
        public Class F64;
        public Class F32;
        public Class String;
        public Class I32;
        public Class I8;
        public Class U32;
        public Class Pointer;
        public Class Any;
        public Dictionary<string, Enum> Enums = new(0);
        public Dictionary<string, Function> Functions = new(0);

        public static Builder Instance;
        public Program Program;
        public Builder(Program program) {
            Program = program;
            Instance = this;
        }

        public void Build() {
            RegisterBuiltinTypes();
            if (Program.HasErrors || Program.Errors.Count > 0) {
                return;
            }
            Console.Write("\n  Building ...");
            var position = Console.GetCursorPosition();
            RegisterClasses();
            RegisterClassesArrays();
            RegisterEnums();
            RegisterFunctions();
            if (Program.HasErrors == false) {
                Program.PrintOk(position);
            }
        }

        private void RegisterClassesArrays() {
            foreach (var cls in Classes.Values) {
                if (cls.ArrayOf != null) {
                    if (Find(cls.ArrayOf.Annotation.Value) is Class type) {
                        cls.ArrayOf.Type = type;
                    } else {
                        Program.AddError(cls.Token, Error.UnknownType);
                    }
                }
            }
        }

        private void RegisterEnums() {
            foreach (var en in Program.Find<Enum>()) {
                if (Classes.ContainsKey(en.Token.Value)) {
                    Program.AddError(en.Token, Error.NameAlreadyExists);
                    continue;
                }
                if (Enums.TryAdd(en.Token.Value, en) == false) {
                    Program.AddError(en.Token, Error.NameAlreadyExists);
                    continue;
                }
            }
        }

        void RegisterBuiltinTypes() {
            //Program.Add<Using>().LoadModule("system");
            Program.Add<Using>().LoadModule("builtin");
        }

        public Class Find(string name, out AST from) {
            from = null;
            if (Classes.TryGetValue(name, out Class cls)) {
                return cls;
            }
            if (Enums.TryGetValue(name, out Enum en)) {
                from = en;
                return en.Type;
            }
            return null;
        }

        public Class Find(string name) => Find(name, out _);

        void RegisterFunctions() {
            foreach (var func in Program.Find<Function>()) {
                RegisterFunction(func);
            }

            foreach (var property in Program.Find<Property>()) {
                if (property.SimpleKind != Property.PropertyKind.None) {
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
            //if (func.HasGenerics) return;
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
                Real = "void",
                IsNative = true,
                IsAny = true,
            });
            foreach (var cls in Program.Find<Class>()) {
                if (Classes.TryAdd(cls.Token.Value, cls) == false) {
                    Program.AddError(cls.Token, Error.NameAlreadyExists);
                    continue;
                }
                cls.ID = Class.CounterID++;
                SetBuiltinTypes(cls);
                SetDefaultConstructor(cls);
            }
            foreach (var cls in Classes.Values.ToArray()) {
                //ValidateClass(cls);
                ValidateBased(cls);
                ValidateInterfaces(cls);
                //if (cls.HasGenerics)
                //    ValidateGenerics(cls, cls.Generics);
            }
        }

        private void SetBuiltinTypes(Class cls) {
            switch (cls.Token.Value) {
                case "bool": Bool = cls; break;
                case "string": String = cls; break;
                case "i32": I32 = cls; break;
                case "u32": U32 = cls; break;
                case "byte": Byte = cls; break;
                case "char": Char = cls; break;
                case "i8": I8 = cls; break;
                case "f64": F64 = cls; break;
                case "f32": F32 = cls; break;
                case "pointer": Pointer = cls; break;
            }
        }

        private void SetDefaultConstructor(Class cls) {
            if (cls is not Interface && /*cls.IsNative == false && */cls.Children.Any(c => c is Constructor) == false) {
                var ctor = cls.Add<Constructor>();
                ctor.Type = cls;
                ctor.Real = cls.Token.Value + "_this";
                ctor.Token = new Token {
                    Value = "this",
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

        bool ValidateClass(Class cls) {
            if (cls == null) return true;
            if (cls.IsBased) {
                if (ValidateBased(cls.Base) == false) {
                    return false;
                }
            }
            if (Classes.ContainsKey(cls.Token.Value) == false) {
                Program.AddError(cls.BaseToken.Token, Error.UnknownType);
                return false;
            }
            ValidateInterfaces(cls);
            //if (cls.HasGenerics) {

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
            if (cls.HasInterfaces == false) return;
            for (int i = 0; i < cls.Interfaces.Count; i++) {
                var inter = cls.Interfaces[i];
                if (Classes.TryGetValue(inter.Token.Value, out Class face) == false) {
                    Program.AddError(inter.Token, Error.UnknownType);
                    continue;
                }
                cls.Interfaces[i] = face as Interface;
            }
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
                            p.Arrays = v.Arrays;
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
                            if (p.Type.IsTemporary) {
                                if (Classes.TryGetValue(p.Type.Token.Value, out Class nt)) {
                                    p.Type = nt;
                                }
                            }
                            buff.Append('_').Append(p.Type.Token.Value);
                        }
                    }
                }
            }
        }
    }
}