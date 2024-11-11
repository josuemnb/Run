using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Run {
    public class Cpp_Transpiler : Transpiler {
        public Cpp_Transpiler(Builder builder) : base(builder) {
            builder.Null.Real = "nullptr";
        }

        public override void Save(string path) {
            if (string.IsNullOrEmpty(Destination) == false) return;
            if (string.IsNullOrEmpty(path)) {
                throw new Exception("Path can't be null or empty");
            }

            Destination = path + ".cpp";
            using var stream = new FileStream(Destination, FileMode.Create);
            Save(stream);
        }

        public override void Save(Stream stream) {
            Writer = new StreamWriter(stream);
            Writer.WriteLine("""
                #include <stdio.h>
                #include <stdlib.h>
                
                #define null nullptr
                
                """);
            SaveAnnotations();
            SaveDeclarations();
            SaveImplementations();
            SaveInitializer();
            Writer.Close();
        }
        public override bool Compile() {
            if (Builder.Program.HasMain == false) {
                Console.WriteLine("\n...No Main");
                return true;
            }
            if (Builder.Program.Main.Children.Count == 0) {
                Console.WriteLine("\n.....Empty");
                return true;
            }
            var libraries = Builder.Program.FindChildren<Library>().Select(l => l.Token.Value).Distinct().ToList();
            var location = AppContext.BaseDirectory;
            //TODO improve this code
            var info = new ProcessStartInfo("g++") {
                Arguments = "-o " + Builder.Program.Token.Value + ".exe " + Destination + " -w -O2 -fpermissive",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var proc = Process.Start(info);
            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                return false;
            }
            return true;
        }
        void SaveDeclarations() {
            SaveClassesPrototypes();
            SaveEnumsPrototypes();
            SaveClassesDeclarations();
            SaveFunctionsPrototypes();
        }
        void SaveImplementations() {
            //SaveClassesInitializers();
            //Writer.WriteLine();
            SaveFunctionsImplementations();
            //Writer.WriteLine();
        }
        private void SaveAnnotations() {
            var headers = new HashSet<string> {
                "malloc.h",
                "stdio.h",
                "stdlib.h",
                "wchar.h",
                "string.h",
                "stddef.h",
            };
            var values = Builder.Program.FindChildren<AST>().Where(a => a.Annotations != null);
            foreach (var ast in values) {
                ast.Annotations.ForEach(annotation => {
                    if (annotation.IsHeader) {
                        if (headers.Add(annotation.Value)) {
                            Writer.Write("#include \"");
                            Writer.Write(annotation.Value);
                            Writer.WriteLine("\"");
                        }
                    }
                });
            }

            //Writer.WriteLine("\nusing namespace std;");
        }
        private void SaveGlobals() {
            SaveGlobals(Builder.Program);
            foreach (var module in Builder.Program.FindChildren<Module>()) {
                SaveGlobals(module);
            }
            Writer.WriteLine();
        }
        void SaveGlobals(Module module) {
            module.Children.ForEach((item) => {
                switch (item) {
                    case Var v:
                        Save(v);
                        Writer.WriteLine(';');
                        break;
                }
            });
        }
        void SaveInitializer() {
            Writer.WriteLine("void run_initializer(int argc, char *argv[]) {");
            Builder.Program.Children.ForEach((item) => {
                switch (item) {
                    case Var v:
                        Writer.Write("\t");
                        Writer.Write(v.Real);
                        SaveInitializer(v);
                        Writer.WriteLine(';');
                        break;
                }
            });
            SaveMain();
            Writer.WriteLine("}");
            Writer.WriteLine("int main(int argc, char *argv[]) {");
            Writer.WriteLine("\trun_initializer(argc, argv);");
            Writer.WriteLine("\treturn 0;");
            Writer.WriteLine("}");
            Writer.WriteLine();
        }
        void SaveMain() {
            if (Builder.Program.Main == null) return;
            if (Builder.Program.Main.Parameters != null) {
                Writer.Write("\tif(argc<");
                Writer.Write(Builder.Program.Main.Parameters.Children.Count + 1);
                Writer.WriteLine(") {");
                Writer.Write("\t\tfprintf(stderr, \"Invalid number of arguments! Expecting:\\n");
                foreach (Parameter param in Builder.Program.Main.Parameters.Children) {
                    Writer.Write("  ");
                    Writer.Write(param.Real);
                    Writer.Write("=<");
                    Writer.Write(param.Type.Real);
                    Writer.Write(">\\n");
                }
                Writer.WriteLine("\");");
                Writer.WriteLine("\t\treturn;");
                Writer.WriteLine("\t}");
                bool hasError = false;
                for (int i = 0; i < Builder.Program.Main.Parameters.Children.Count; i++) {
                    var param = Builder.Program.Main.Parameters.Children[i] as Parameter;
                    if (param.Type.IsPrimitive == false && param.Type != Builder.String) {
                        Builder.Program.AddError(param.Token, Error.InvalidExpression);
                        hasError = true;
                    }
                    Writer.Write("\t");
                    Writer.Write(param.Type.Real);
                    if (param.Arguments != null) {
                        Writer.Write('*');
                    }
                    Writer.Write(' ');
                    Writer.Write(param.Real);
                    Writer.WriteLine(" = 0;");
                }
                if (hasError) return;
                Writer.WriteLine("\tfor(int i=1;i<argc;i++) {");
                Writer.Write("\t\t");
                Writer.Write(Builder.String.Real);
                Writer.WriteLine(" arg = argv[i];");
                for (int i = 0; i < Builder.Program.Main.Parameters.Children.Count; i++) {
                    var p = Builder.Program.Main.Parameters.Children[i] as Parameter;
                    Writer.Write("\t\tif(arg.rfind(\"");
                    Writer.Write(p.Token.Value);
                    Writer.WriteLine("=\")==0) {");
                    Writer.Write("\t\t\t");
                    Writer.Write(p.Real);
                    Writer.Write(" = ");
                    bool isString = false;
                    bool moreParams = false;
                    switch (p.Type.Real) {
                        case "string":
                            isString = true;
                            break;
                        case "f32":
                            Writer.Write("(float)atof(");
                            break;
                        case "f64":
                            Writer.Write("atof(");
                            break;
                        case "i32":
                        case "i16":
                        case "i8":
                            Writer.Write("atoi(");
                            break;
                        case "i64":
                            Writer.Write("strtoll(");
                            moreParams = true;
                            break;
                        case "u32":
                        case "u16":
                        case "u8":
                            Writer.Write("strtoul(");
                            moreParams = true;
                            break;
                        case "u64":
                            Writer.Write("strtoull(");
                            moreParams = true;
                            break;

                    }
                    Writer.Write("arg.substr(");
                    Writer.Write(p.Token.Value.Length + 1);
                    if (moreParams) {
                        Writer.WriteLine(",0x0,10");
                    }
                    Writer.WriteLine(isString ? ");" : "));");
                    Writer.WriteLine("\t\t}");
                }
                Writer.WriteLine("\t}");

            }
            Writer.Write("\trun_main(");
            if (Builder.Program.Main.Parameters != null) {
                for (int i = 0; i < Builder.Program.Main.Parameters.Children.Count; i++) {
                    if (i > 0) {
                        Writer.Write(", ");
                    }
                    var p = Builder.Program.Main.Parameters.Children[i] as Parameter;
                    if (p.Type is Class c && c.IsPrimitive == false) {
                        Writer.Write('&');
                    }
                    Writer.Write(p.Real);
                }
            }
            Writer.WriteLine(");");
        }
        void SaveStaticClassMembers(Class cls) {
            if (cls.Children == null) return;
            foreach (var child in cls.Children) {
                if (child is Field f && f.AccessType == AccessType.STATIC) {
                    f.Real = cls.Token.Value + f.Real;
                    Save(f);
                    if (f.Initializer != null) {
                        Writer.Write(" = ");
                        Save(f.Initializer);
                    } else {
                        Writer.Write(" = 0");
                    }
                    Writer.WriteLine(";");
                }
            }
        }
        void SaveClassesDeclarations() {
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                SaveStaticClassMembers(cls);
                if (cls.Usage == 0 || cls.IsPrimitive || cls.AccessType == AccessType.STATIC) {
                    continue;
                }
                if (cls.IsEnum) {
                    SaveEnum(cls);
                } else {
                    SaveClassDeclaration(cls);
                }
            }
            Writer.WriteLine();
        }

        void SaveEnum(Class cls) {
            foreach (EnumMember child in cls.Children) {
                SaveEnumDeclaration(child);
            }
        }

        void SaveNative(Class cls) {
            if (cls.Children == null) return;
            foreach (var child in cls.Children) {
                switch (child) {
                    case Property property:
                        Save(property, true);
                        break;
                    case Function func when func is not Constructor:
                        if (func.IsNative) {
                            if (func.NativeNames?.Count > 0) {
                                if (func.NativeNames[0] == func.Token.Value) {
                                    break;
                                }
                            }
                        }
                        func.IsExtension = true;
                        Save(func, true);
                        break;
                }
            }
        }

        void SaveClassDeclaration(Class cls) {
            if (cls.IsNative) {
                SaveNative(cls);
                return;
            }
            Writer.Write("class ");
            Writer.Write(cls.Real);
            if (cls.IsBased) {
                Writer.Write(": public ");
                Writer.Write(cls.BaseType.Real);
            }
            Writer.WriteLine(" {");
            if (cls.Children != null) {
                foreach (var child in cls.Children) {
                    switch (child) {
                        case Property property:
                            Save(property);
                            break;
                        case Function func:
                            Save(func, true);
                            break;
                        case Var v:
                            if (v.AccessType == AccessType.STATIC) {
                                continue;
                            }
                            Writer.Write("\t");
                            Save(v);
                            if (v.Initializer != null) {
                                Writer.Write(" = ");
                                Save(v.Initializer);
                            }
                            Writer.WriteLine(";");
                            break;
                    }
                }
            }
            Writer.WriteLine("};");
        }
        private void SaveClassProperties() {
            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsEnum) continue;
                foreach (var property in cls.FindChildren<GetterSetter>()) {
                    if (property.Usage == 0) continue;
                    if (property.SimpleKind != PropertyKind.None) {
                        continue;
                    }
                    if (property.Getter != null) {
                        Save(property.Getter);
                        Writer.WriteLine(";");
                    }
                    if (property.Setter != null) {
                        Save(property.Setter);
                        Writer.WriteLine(";");
                    }
                }
            }
        }
        bool SaveClassesPrototypes() {
            bool ok = false;
            Writer.WriteLine();
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                if (cls.IsNative || cls.AccessType == AccessType.STATIC) continue;
                if (cls.IsEnum) continue;
                if (cls.Usage == 0) continue;
                Writer.Write("class ");
                Writer.Write(cls.Real);
                Writer.WriteLine(";");
                ok = true;
            }
            return ok;
        }
        bool SaveEnumsPrototypes() {
            bool ok = false;
            Writer.WriteLine();
            foreach (var cls in Builder.Classes.Values) {
                if (cls is not Enum enm) continue;
                if (enm.IsEnum == false) {
                    continue;
                }
                if (enm.Usage == 0) {
                    continue;
                }
                Writer.Write("#define ");
                Writer.Write(enm.Real);
                Writer.Write(" ");
                Writer.WriteLine(enm.BaseType.Real);
                SaveEnum(enm);
                ok = true;
            }
            if (ok) {
                Writer.WriteLine();
            }
            return ok;
        }
        void SaveEnumDeclaration(EnumMember e) {
            var parent = e.Parent as Enum;
            Writer.Write(parent.Real);
            if (e.Type.IsPrimitive == false) {
                Writer.Write('*');
            }
            Writer.Write(' ');
            Save(e);
            Writer.Write(" = ");
            if (e.Content != null) {
                if (e.Content is LiteralExpression literal && e.Type == Builder.String) {
                    Writer.Write(literal.Token.Value);
                } else {
                    Save(e.Content);
                }
            } else {
                Writer.Write(parent.Children.IndexOf(e));
            }
            Writer.WriteLine(";");
        }
        bool SaveFunctionsPrototypes() {
            bool ok = false;
            foreach (Function func in Builder.Functions.Values) {
                if (func.IsNative || func is Constructor ctor && ctor.Type.AccessType == AccessType.STATIC) {
                    continue;
                }
                if (func.Usage == 0) {
                    continue;
                }
                if (SaveDeclaration(func, false)) Writer.WriteLine(";");
                ok = true;
            }
            Writer.WriteLine();
            return ok;
        }
        void SaveFunctionsImplementations() {
            foreach (Function func in Builder.Functions.Values) {
                if (func.IsNative || func is Constructor ctor && ctor.Type.AccessType == AccessType.STATIC) {
                    continue;
                }
                if (func.Usage == 0) {
                    continue;
                }
                Save(func);
            }
        }

        void Save(AST exp) {
            switch (exp) {
                case Main m: Save(m); break;
                case LiteralExpression l: Save(l); break;
                case IdentifierExpression i: Save(i); break;
                case IndexerExpression i: Save(i); break;
                case DotExpression d: Save(d); break;
                case IsExpression i: Save(i); break;
                case UnaryExpression u: Save(u); break;
                case ParentesesExpression p: Save(p); break;
                case PropertySetter p: Save(p); break;
                case CallExpression c: Save(c); break;
                case NewExpression n: Save(n); break;
                case SizeOf s: Save(s); break;
                case TypeOf t: Save(t); break;
                case AsExpression a: Save(a); break;
                case Ref r: Save(r); break;
                case ThisExpression t: Save(t); break;
                case BinaryExpression b: Save(b); break;
                case TernaryExpression t: Save(t); break;
                case Switch s: Save(s); break;
                case Case c: Save(c); break;
                case For f: Save(f); break;
                case Else e: Save(e); break;
                case If i: Save(i); break;
                case Goto g: Save(g); break;
                case Break b: Save(b); break;
                case Continue c: Save(c); break;
                case Return r: Save(r); break;
                case Delete d: Save(d); break;
                case Scope s: Save(s); break;
                case Label l: Save(l); break;
                case Parameter p: Save(p); break;
                case Indexer i: Save(i); break;
                case Property p: Save(p); break;
                case EnumMember e: Save(e); break;
                case Constructor c: Save(c); break;
                case GetterSetter g: Save(g); break;
                case Function f: Save(f); break;
                case Var v: Save(v); break;
                case Block b: SaveBlock(b); break;
            }
        }

        void Save(ThisExpression t) {
            if (t.FindParent<Function>() is Function f) {
                if (f.IsExtension) {
                    Writer.Write('_');
                }
            }
            Writer.Write("this");
        }

        void Save(Main exp) {

        }

        void Save(IndexerExpression exp) {
            if (exp == null) return;
            Save(exp.Left);
            Writer.Write('[');
            Save(exp.Right);
            Writer.Write(']');
        }

        void Save(DotExpression exp) {
            if (exp == null) return;
            Save(exp.Left);
            Writer.Write(exp.Left.Type?.IsEnum ?? false ? "" : "->");
            Save(exp.Right);
        }

        void Save(BinaryExpression exp) {
            if (exp == null) return;
            Save(exp.Left);
            Writer.Write(exp.Token.Value);
            Save(exp.Right);
        }

        void Save(UnaryExpression exp) {
            if (exp == null) return;
            if (exp.AtRight == false) Writer.Write(exp.Token.Value);
            Save(exp.Content);
            if (exp.AtRight) Writer.Write(exp.Token.Value);
        }

        void Save(EnumMember exp) {
            if (exp == null) return;
            Writer.Write(exp.Parent.Real);
            Writer.Write('_');
            Writer.Write(exp.Token.Value);
        }

        void Save(LiteralExpression exp) {
            if (exp == null) return;
            Writer.Write(exp.Token?.Value);
        }

        void Save(ParentesesExpression exp) {
            if (exp == null) return;
            Writer.Write('(');
            Save(exp.Content);
            Writer.Write(')');
        }

        void Save(Switch exp) {
            if (exp == null) return;
            if (exp.Type == null) return;
            if (exp.SameType && exp.Type.IsPrimitive) {
                Writer.Write("switch (");
                Save(exp.Expression);
                Writer.WriteLine(") {");
                SaveBlock(exp);
                Writer.WriteLine("}");
                return;
            }
            SaveBlock(exp);
        }

        void Save(Case exp) {
            if (exp == null) return;
            if (exp.SameType && exp.Type.IsPrimitive) {
                foreach (var p in exp.Expressions) {
                    Writer.Write("case (");
                    Save(p);
                    Writer.Write("):");
                }
            } else {
                var sw = exp.FindParent<Switch>();
                if (exp.Index > 0) Writer.Write("else ");
                Writer.Write("if(");
                Save(sw.Expression);
                for (int i = 0; i < exp.Expressions.Count; i++) {
                    var p = exp.Expressions[i];
                    if (i > 0) {
                        Writer.Write(" || ");
                    }
                    Writer.Write("(");
                    if (p is UnaryExpression) {
                    } else {
                        Writer.Write(" == ");
                    }
                    Save(p);
                    Writer.Write(")");
                }
                Writer.Write(")");
            }
            Writer.WriteLine(" {");
            SaveBlock(exp);
            Writer.WriteLine("}");
        }

        void Save(TernaryExpression exp) {
            if (exp == null) return;
            Save(exp.Condition);
            Writer.Write(" ? ");
            Save(exp.True);
            Writer.Write(" : ");
            Save(exp.False);
        }

        void SaveBlock(Block block) {
            if (block == null) return;
            if (block.Defers.Count > 0) {
                Writer.Write("int __DEFER_STAGE__");
                Writer.Write(block.Defers[0].ID);
                Writer.WriteLine(" = 0;");
            }
            for (int i = 0; i < block.Children.Count; i++) {
                var child = block.Children[i];
                if (child is Parameter) continue;
                Save(child);
                Writer.Write(child is Expression ? ";\n" : "");
                Writer.Write(child is Var ? ";\n" : "");
            }
            if (block.Defers.Count > 0) {
                Writer.Write("__DEFER__");
                Writer.Write(block.Defers[0].ID);
                Writer.WriteLine(":\n;");
                var ID = block.Defers[0].ID;
                for (int i = block.Defers.Count - 1; i >= 0; i--) {
                    var defer = block.Defers[i];
                    Writer.Write("if (__DEFER_STAGE__");
                    Writer.Write(ID);
                    Writer.Write(" >= ");
                    Writer.Write(defer.Token.Value);
                    Writer.WriteLine(") {");
                    SaveBlock(defer);
                    Writer.WriteLine("}");
                }
                if (block.Parent is Block p && p is not Class) {
                    if (p.Defers.Count > 0) {
                        Writer.Write("goto __DEFER__");
                        Writer.Write(p.Defers[0].ID);
                        Writer.WriteLine(";");
                    }
                }
            }
        }

        Class SaveReturnType(Function exp) {
            if (exp == null) return null;
            var cls = exp.Parent as Class;
            if (cls == null && exp.Parent is GetterSetter p) {
                cls = p.Parent as Class;
            }
            if (exp is Constructor) {
                //    Writer.Write(cls.Real);
                //    if (cls.IsPrimitive == false)
                //        Writer.Write("*");
            } else {
                SaveType(exp.Type);
                if (exp.TypeArray) {
                    Writer.Write("*");
                }
            }
            return cls;
        }

        void SaveAcessType(AST ast) {
            if (ast == null) return;
            switch (ast.AccessType) {
                case AccessType.STATIC:
                    Writer.Write("static ");
                    break;
            }
        }

        void SaveAccessModifier(AST ast) {
            if (ast == null) return;
            switch (ast.AccessModifier) {
                case AccessModifier.PUBLIC:
                    Writer.Write("public: ");
                    break;
                case AccessModifier.PRIVATE:
                    Writer.Write("private: ");
                    break;
                case AccessModifier.PROTECTED:
                    Writer.Write("protected: ");
                    break;
            }
        }

        bool SaveDeclaration(Function exp, bool allowConstructors) {
            if (exp == null) return false;
            if (allowConstructors == false) {
                if (exp is Constructor) return false;
                if (exp.Parent is Property) return false;
            }
            if (allowConstructors && exp.IsExtension == false) SaveAccessModifier(exp);

            var cls = SaveReturnType(exp);
            Writer.Write(" ");
            if (exp is Constructor) {
                Writer.Write(cls.Real);
            } else if (cls == null || exp.IsExtension) {
                Writer.Write(exp.Real);
            } else if (cls != null) {
                Writer.Write(exp.Token.Value);
            } else {

            }
            Writer.Write("(");
            bool started = false;
            if (exp.IsExtension) {
                Writer.Write(cls.Real);
                if (cls.IsPrimitive == false) {
                    Writer.Write("*");
                }
                Writer.Write(" _this");
                started = true;
            }
            if (exp.Parameters != null) {
                foreach (var item in exp.Parameters.Children) {
                    if (item is Parameter p) {
                        if (started) {
                            Writer.Write(", ");
                        }
                        if (p.Type is Class c && c.IsPrimitive == false) {
                            p.IsScoped = false;
                        }
                        Save(p);
                        started = true;
                    }
                }
            }
            Writer.Write(")");
            return true;
        }

        void Save(Function exp, bool allowConstructors = false) {
            if (exp == null) return;
            if (exp.IsNative) {
                return;
            }
            if (SaveDeclaration(exp, allowConstructors) == false) return;
            if (exp.Pointer != null) {
                Writer.Write(" = ");
                Writer.Write(exp.Pointer.Value);
                return;
            }
            Writer.WriteLine(" {");
            if (exp is Constructor) {
                var cls = exp.Parent as Class;
                if (cls.IsBased) {
                    bool found = false;
                    if (exp.Parameters != null && exp.Parameters.Children.Count > 0) {
                        foreach (var c in cls.FindChildren<Constructor>()) {
                            if (c.Parameters != null && c.Parameters.Children.Count == exp.Parameters.Children.Count) {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found == false) {
                        Writer.Write(cls.BaseType.Real);
                        Writer.WriteLine("_this(this);");
                    }
                }
            }
            if (exp.HasVariadic) {
                SaveVariadic(exp);
            }
            SaveParametersMembers(exp);
            if (exp.IsArrow && (exp.Children.Count > 0 && (exp.Parameters == null || exp.Parameters.Children.Count == 0) || exp.Children.Count > 1 && exp.Parameters != null && exp.Parameters.Children.Count > 0)) {
                if (exp.Type != null) {
                    Writer.Write("return ");
                }
                if ((exp.Parameters == null || exp.Parameters.Children.Count == 0) && exp.Children[0] is AST ast) {
                    Save(ast);
                } else if (exp.Parameters != null && exp.Children.Count > 1 && exp.Children[1] is AST a) {
                    Save(a);
                }
                Writer.WriteLine(";\n}");
                return;
            }
            if (exp.Type != null && exp is not Constructor) {
                SaveReturnType(exp);
                Writer.WriteLine(" __RETURN__ = 0;");
            }
            SaveBlock(exp);
            if (exp is Constructor) {
            } else {
                Writer.WriteLine("__DONE__:\n;");
                Writer.Write("return");
                if (exp.Type != null) {
                    Writer.Write(" __RETURN__");
                }
                Writer.WriteLine(";");
            }
            Writer.WriteLine("}\n");
        }

        public void SaveType(Class type) {
            if (type == null) {
                Writer.Write("void");
                return;
            }
            Writer.Write(type.Real ?? type.Token.Value);
            if (type.IsPrimitive == false) {
                Writer.Write("*");
            }
        }

        void SaveVariadic(Function exp) {
            if (exp == null) return;
            var vary = exp.Parameters.Children.Last() as Parameter;
            Writer.Write("_array* ");
            Writer.Write(vary.Real);
            Writer.Write(" = array_this_i32_i32(array_initializer(NEW(_array, 1, 2)), (_i32){0, sizeof(");
            SaveType(vary.Type);
            Writer.Write(")}, len");
            Writer.Write(vary.Real);
            Writer.WriteLine(");");
            Writer.Write("va_list args");
            Writer.Write(vary.Real);
            Writer.WriteLine(";");
            Writer.Write("va_start(args");
            Writer.Write(vary.Real);
            Writer.Write(", len");
            Writer.Write(vary.Real);
            Writer.WriteLine(");");
            Writer.WriteLine("for(int i=0;i<len" + vary.Real + ".value;i++) {");
            Writer.Write("array_add_any (");
            Writer.Write(vary.Real);
            Writer.Write(",(_any){0, va_arg(args");
            Writer.Write(vary.Real);
            Writer.WriteLine(", void*)});");
            Writer.WriteLine("}");
            Writer.Write("va_end(args");
            Writer.Write(vary.Real);
            Writer.WriteLine(");");
            Writer.Write(vary.Real);
            Writer.WriteLine("->_size = len" + vary.Real + ";");
        }

        void SaveParametersMembers(Function exp) {
            if (exp == null) return;
            if (exp.Parameters == null) return;
            foreach (var child in exp.Parameters.Children) {
                if (child is Parameter param && param.IsMember) {
                    Writer.Write("this->");
                    Writer.Write(param.Real);
                    Writer.Write(" = ");
                    Writer.Write(param.Real);
                    Writer.WriteLine(";");
                }
            }
        }

        void Save(Indexer exp) {
            if (exp == null) return;
            Save(exp.Getter);
            Save(exp.Setter);
        }

        void Save(Parameter exp) {
            if (exp == null) return;
            Save(exp as Var);
        }

        void Save(Property exp, bool isExtension = false) {
            if (exp == null) return;
            if (exp.Initializer != null) {
                if (isExtension == false) {
                    Save(exp as Var);
                }
                return;
            }
            if (exp.Getter != null) {
                exp.Getter.IsExtension = isExtension;
                Save(exp.Getter, true);
            }
            if (exp.Setter != null) {
                exp.Setter.IsExtension = isExtension;
                Save(exp.Setter, true);
            }
        }

        void Save(PropertySetter exp) {
            if (exp == null) return;
            Writer.Write(exp.Real);
            Writer.Write('(');
            Save(exp.This);
            Writer.Write(')');
        }

        void Save(Var exp) {
            if (exp == null) return;
            if (exp.Type == null) {
                if (exp.Initializer != null) {
                    exp.Program.Validator?.Validate(exp.Initializer);
                    exp.Type = exp.Initializer.Type;
                }
            }
            if (exp.Type == null) {
                Error.NullType(exp);
                return;
            }
            Writer.Write(exp.Type.Real ?? exp.Type.Token.Value);
            Writer.Write(' ');
            switch (exp.Initializer) {
                case NewExpression ne:
                    exp.TypeArray = ne.Content is ArrayCreationExpression;
                    break;
                case CallExpression call when call.Function != null:
                    exp.TypeArray = call.Function.TypeArray;
                    break;
            }
            if (exp.IsScoped == false) Writer.Write('*');
            if (exp.TypeArray) Writer.Write("*");
            if (exp.Arguments != null) {
                exp.TypeArray = false;
                //Writer.Write('*');
            }
            Writer.Write(exp.Real ?? exp.Token.Value);
            if (exp.FindParent<Function>() != null) {
                SaveInitializer(exp, exp.TypeArray);
            }
        }
        void SaveInitializer(Var exp, bool array = true) {
            if (exp == null) return;
            if (array && exp.Arguments != null) {
                Writer.Write('[');
                if (exp.Arguments.Count > 0) {
                    Save(exp.Arguments[0]);
                }
                Writer.Write(']');
            } else if (exp.Initializer != null && exp.Parent is not Class) {
                Writer.Write(" = ");
                Save(exp.Initializer);
            }
        }

        void Save(Label exp) {
            if (exp == null) return;
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(':');
        }

        void Save(Break exp) {
            if (exp == null) return;
            Writer.WriteLine("break;");
        }

        void Save(Continue exp) {
            if (exp == null) return;
            Writer.WriteLine("continue;");
        }

        void Save(Else exp) {
            if (exp == null) return;
            Writer.Write("else ");
            if (exp.Condition != null) {
                Save(exp.Condition);
            }
            Writer.WriteLine('{');
            SaveBlock(exp);
            Writer.WriteLine('}');
        }

        void Finish(For exp) {
            if (exp == null) return;
            if (exp.Children.Count > 0) {
                Writer.WriteLine(") {");
                SaveBlock(exp);
                Writer.WriteLine("}");
            } else {
                Writer.WriteLine(");");
            }
        }

        void Save(Scope exp) {
            if (exp == null) return;
            Writer.Write("SCOPE(");
            Writer.Write(exp.Token.Value);
            Writer.Write(")");
        }

        void Save(TypeOf exp) {
            if (exp == null) return;
            if (exp.Type != null && exp.Type.ID >= 0 && exp.Type.ID < Class.CounterID) {
                Writer.Write("__TypesMap__[");
                Writer.Write(exp.Type.ID);
                Writer.Write("]");
                return;
            }
            Builder.Program.AddError(exp.Token, Error.InvalidExpression);
        }

        void Save(SizeOf exp) {
            if (exp == null) return;
            if (exp.Content is IdentifierExpression id) {
                if (id.From is Var p && p.Arguments != null) {
                    Writer.Write("SIZEOF(");
                    Writer.Write(id.Token.Value);
                    Writer.Write(")");
                    return;
                } else if (id.Type != null) {
                    Writer.Write("sizeof(");
                    Writer.Write(id.Type.Real);
                    Writer.Write(')');
                    return;
                }
            } else if (exp.Content is ThisExpression) {
                Writer.Write("SIZEOF(this)");
                return;
            }
            Writer.Write("sizeof(");
            Save(exp.Content);
            Writer.Write(')');
        }


        void Save(Return exp) {
            if (exp == null) return;
            var func = exp.FindParent<Function>();
            if (func.Type != null && func is not Constructor) {
                Writer.Write("__RETURN__ = ");
                Save(exp.Content);
                Writer.WriteLine(";");
            }
            var block = exp.Parent as Block;
            if (block.Defers.Count == 0) {
                Writer.Write("goto __DONE__");
            } else {
                Writer.Write("goto __DEFER__");
                Writer.Write(block.Defers[0].ID);
            }
            Writer.WriteLine(";");
        }

        void Save(Ref exp) {
            if (exp == null) return;
            if (exp.Content.Type.IsPrimitive) {
                Writer.Write("&(");
            } else
            if (exp.Content.Type.IsNumber) {
                Writer.Write("&(");
            } else {
                Writer.Write("*(");
            }
            Save(exp.Content);
            Writer.Write(')');
        }

        void Save(For exp) {
            if (exp == null) return;
            switch (exp.Stage) {
                case -1:
                    Writer.Write("while(1");
                    break;
                case 0 when exp.Start is Expression condition:
                    Writer.Write("while(");
                    Save(condition);
                    break;
                case 0 when exp.Start is Var var && exp.Condition == null:
                    Writer.Write("for(");
                    Save(var);
                    Writer.Write(";;");
                    Writer.Write(var.Real);
                    Writer.Write("++");
                    break;
                case 0 when exp.Start is Var var && exp.Condition is Iterator iterator:
                    Writer.Write("for(int ");
                    Writer.Write(var.Real);
                    Writer.Write("_it = 0; ");
                    Writer.Write(var.Real);
                    Writer.Write("_it < ");
                    var id = iterator.Content as IdentifierExpression;
                    Writer.Write(id.Real);
                    Writer.Write("->_size.value; ");
                    Writer.Write(var.Real);
                    Writer.WriteLine("_it++) {");
                    Writer.Write("_any ");
                    Writer.Write(var.Real);
                    Writer.Write(" = ");
                    Writer.Write(id.Real);
                    Writer.Write("->_items[");
                    Writer.Write(var.Real);
                    Writer.WriteLine("_it];");
                    SaveBlock(exp);
                    Writer.WriteLine("}");
                    return;
                default:
                    Writer.Write("for(");
                    Save(exp.Start);
                    Writer.Write(';');
                    Save(exp.Condition);
                    Writer.Write(';');
                    Save(exp.Step);
                    break;
            }
            Finish(exp);
        }

        void Save(If exp) {
            if (exp == null) return;
            Writer.Write("if(");
            Save(exp.Condition);
            Writer.WriteLine(") {");
            SaveBlock(exp);
            Writer.WriteLine("}");
        }

        void Save(Goto exp) {
            if (exp == null) return;
            Writer.Write("goto ");
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(';');
        }

        void Save(Delete exp) {
            if (exp == null) return;
            for (int i = 0; i < exp.Block.Children.Count; i++) {
                var child = exp.Block.Children[i];
                if (child is IdentifierExpression id && id.Type is Class cls && cls.Dispose != null) {
                    Writer.Write("(");
                    Writer.Write(id.Token.Value);
                    Writer.WriteLine(");");
                }
                Writer.Write("delete ");
                Save(child);
                if (i < exp.Block.Children.Count - 1) {
                }
                Writer.WriteLine(';');
            }
        }

        void Save(NewExpression exp) {
            if (exp == null) return;
            if (exp.Content is ArrayCreationExpression array) {
                Writer.Write("new ");
                Writer.Write(array.Type.Real);
                Writer.Write('[');
                Save(array.Content);
                Writer.Write(']');
            } else if (exp.Content is ConstructorExpression ctor) {
                if (exp.IsScoped == false) Writer.Write("new ");
                Writer.Write(exp.Type.Real);
                Writer.Write('(');
                for (int i = 0; i < ctor.Arguments.Count; i++) {
                    if (i > 0) Writer.Write(',');
                    Save(ctor.Arguments[i]);
                }
                Writer.Write(')');
            } else {
                Debugger.Break();
            }
        }

        void Save(IdentifierExpression exp) {
            if (exp == null) return;
            if (exp.From != null) {
                switch (exp.From) {
                    case Field f: {
                            if (f.AccessType != AccessType.STATIC && (exp.Parent is not DotExpression || exp.Parent is DotExpression dot && dot.Left == exp)) {
                                Writer.Write("this->");
                            }
                        }
                        break;
                    case GetterSetter p: {
                            if (p.AccessType != AccessType.STATIC && (exp.Parent is not DotExpression || exp.Parent is DotExpression dot && dot.Left == exp)) {
                                Writer.Write(p.Getter.Real);
                                Writer.Write("(");
                                Writer.Write("this");
                                Writer.Write(")");
                                return;
                            }
                        }
                        break;
                }
                Writer.Write(exp.From.Real ?? exp.From.Token.Value);
                return;
            }
            Writer.Write(exp.Real ?? exp.Token.Value);
        }

        void Save(IsExpression exp) {
            if (exp == null) return;
            Writer.Write("((");
            Save(exp.Left);
            Writer.Write(").reflection->id==reflection");
            Writer.Write(exp.Right.Type.Real);
            Writer.Write(".id)");
        }

        void Save(AsExpression exp) {
            if (exp == null) return;
            if (exp.Type == null) return;
            if (exp.Type.IsPrimitive) {
                //Writer.Write("*");
            }
            Writer.Write("(");
            //SaveType(writer, Type);
            if (exp.Type.IsPrimitive) {
                //Writer.Write("*");
            }
            if (exp.IsArray) {
                Writer.Write("*");
            }
            Writer.Write(")");
            Save(exp.Left);
        }

        void SaveNative(CallExpression exp) {
            if (exp == null) return;
            Writer.Write(exp.Function.NativeNames[0]);
            Writer.Write('(');
            var args = exp.Function.NativeNames[1];
            for (int i = 0; i < exp.Arguments.Count; i++) {
                var param = exp.Function.Parameters.Children[i];
                var value = exp.Arguments[i];
                if (value == null) {
                    args = args.Replace("$" + param.Token.Value, "0");
                } else {
                    var memory = new StringWriter();
                    Save(value);
                    args = args.Replace("$" + param.Token.Value, memory.ToString());
                }
            }
            if (args.Contains('$')) {
                if (exp.Function.HasVariadic) {
                    var vary = exp.Function.Parameters.Children[^1];
                    if (args.Contains("$" + vary.Token.Value)) {
                        args = args.Replace("$" + vary.Token.Value, "");
                        if (args.EndsWith(',')) args = args[..^1];
                    } else {
                        Builder.Program.AddError(exp.Token, Error.UnknownFunctionNameOrWrongParamaters);
                    }
                }
            }
            Writer.Write(args);
            Writer.Write(')');
        }

        void SaveAcess(CallExpression exp) {
            if (exp.Function.AccessType == AccessType.STATIC) {
                Writer.Write("::");
                return;
            } else if (exp.Caller is IdentifierExpression id && id.From is Var v) {
                if (v.IsScoped) {
                    Writer.Write('.');
                    return;
                }
            }
            Writer.Write("->");
        }

        void SaveBy(ValueType vt) {
            switch (vt) {
                case IdentifierExpression id when id.From is Var v:
                    if (v.IsScoped) {
                        Writer.Write('&');
                        return;
                    }
                    break;
                case Var v:
                    if (v.IsScoped) {
                        Writer.Write('&');
                        return;
                    }
                    break;
            }
        }

        void Save(CallExpression exp) {
            if (exp == null) return;
            if (exp.Function.IsNative && exp.Function.NativeNames?.Count >= 2) {
                SaveNative(exp);
                return;
            }
            bool started = false;
            if (exp.Caller != null && exp.Function.IsExtension == false) {
                SaveCaller(exp);
                Writer.Write(exp.Function.Token.Value);
                Writer.Write('(');
            } else if (exp.Function.IsExtension) {
                if (exp.Function.IsNative) {
                    SaveCaller(exp, false);
                    Writer.Write(exp.Function.NativeNames[0]);
                    Writer.Write('(');
                } else {
                    Writer.Write(exp.Function.Real);
                    Writer.Write('(');
                    started = SaveCaller(exp, true, false);
                }
            } else {
                Writer.Write(exp.Function.Token.Value);
                Writer.Write('(');
            }
            SaveParameters(exp, started);
            Writer.Write(')');
        }

        bool SaveCaller(CallExpression exp, bool by = true, bool access = true) {
            if (exp == null) return false;
            if (exp.Caller != null) {
                //if (by) SaveBy(exp.Caller);
                Save(exp.Caller);
                if (access) SaveAcess(exp);
            }
            return true;
        }

        private void SaveParameters(CallExpression exp, bool started = false) {
            for (int i = 0; i < exp.Arguments.Count; i++) {
                if (started) {
                    Writer.Write(", ");
                }
                if (exp.Function.HasVariadic && i == exp.Function.Parameters.Children.Count - 1) {
                    Writer.Write(exp.Arguments.Count - exp.Function.Parameters.Children.Count + 1);
                }
                Save(exp.Arguments[i]);
                started = true;
            }
        }

        void Save(Default exp) {
            if (exp == null) return;
            if (exp.SameType && exp.Type.IsPrimitive) {
                Writer.Write("default:");
            } else {
                Writer.Write("else ");
            }
            Writer.WriteLine(" {");
            SaveBlock(exp);
            Writer.WriteLine("}");
        }

        void Save(Defer exp) {
            if (exp == null) return;
            Writer.Write("__DEFER_STAGE__");
            Writer.Write(exp.ID);
            Writer.Write(" = ");
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(";");
        }
    }
}