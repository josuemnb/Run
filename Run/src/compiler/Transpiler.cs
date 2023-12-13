using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Run {
    public class Transpiler {
        Builder Builder;
        TextWriter Writer;
        string Destination;
        public Transpiler(Builder builder) {
            Builder = builder;
        }
        public bool Save(string path) {
            Destination = path;
            using var stream = new FileStream(path, FileMode.Create);
            return Save(stream);
        }
        public bool Save(Stream stream) {
            Writer = new StreamWriter(stream);
            Writer.WriteLine("""
                #include <malloc.h>
                #include <stdio.h>
                #include <setjmp.h>
                #include <stdlib.h>
                #include <wchar.h>
                #include <string.h>
                #include <stddef.h>
                #include <signal.h>
                """);
            SaveAnnotations();
            Writer.WriteLine("""
                //#define NEW(T,total,id) (T*)AllocType(sizeof(T)*total,id)
                #define NEW(T,total,id) (T*)malloc(sizeof(T)*total)
                #define SCOPE(T)  &((T){0})
                //#define DELETE(V) Free(V); V = 0
                #define DELETE(V) free(V); V = 0
                #define CAST(T,exp) ((T)(exp))
                #define SIZEOF(V) (int)((char *)(&V+1)-(char*)(&V))
                #define CONVERT(T,ptr) *((T*)ptr)

                #define type typedef struct
                #define false 0
                #define true !false

                #define TRY(j) do { jmp_buf j; switch (setjmp(j)) { case 0: 
                #define CATCH(x) break; case x:
                #define ENDTRY } } while (0)
                #define THROW(j,x) longjmp(j, x)
                """);
            SaveDeclarations();
            SaveReflection();
            SaveChecks();
            SaveImplementations();
            SaveInitializer();
            Writer.Close();
            return Compile();
        }

        bool Compile() {
            Console.Write("  Compiling ...");
            if (Builder.Program.HasMain == false) {
                Console.WriteLine("...No Main");
                return true;
            }
            if (Builder.Program.Main.Children.Count == 0) {
                Console.WriteLine(".....Empty");
                return true;
            }
            var libraries = Builder.Program.Find<Library>().Select(l => l.Token.Value).Distinct().ToList();
            var location = Assembly.GetExecutingAssembly().Location;
            //TODO improve this code
            var tcc = Directory.EnumerateFiles(Path.GetDirectoryName(location), "tcc.exe", SearchOption.AllDirectories).FirstOrDefault();
            var info = new ProcessStartInfo(tcc) {
                Arguments = "-I" + Path.GetDirectoryName(location) + "/lib " + (Builder.Program.HasMain ? "-o " : "-c ") + "..\\" + Builder.Program.Token.Value + ".exe " + Destination + " -w " + (libraries.Count > 0 ? " -L" + string.Join(" -L", libraries.Select(Path.GetDirectoryName)) + "\"" + " -l\"" + string.Join(" -l\"", libraries.Select(Path.GetFileName)) : ""),
                WindowStyle = ProcessWindowStyle.Hidden,
                //CreateNoWindow = true,
                //UseShellExecute = false,
            };
            var proc = Process.Start(info);
            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                return false;
            }
            Console.WriteLine(".....OK");
            return true;
        }
        private void SaveReflection() {
            Writer.WriteLine("typedef struct ReflectionArgument {");
            Writer.WriteLine("\tconst char* name;");
            Writer.WriteLine("\tint iD;");
            Writer.WriteLine("\tint array;");
            Writer.WriteLine("} ReflectionArgument;\n");

            Writer.WriteLine("typedef struct ReflectionMember {");
            Writer.WriteLine("\tconst char* name;");
            Writer.WriteLine("\tint offset;");
            Writer.WriteLine("\tint kind;");
            Writer.WriteLine("\tint iD;");
            Writer.WriteLine("\tint array;");
            Writer.WriteLine("\tvoid* function;");
            Writer.WriteLine("\tint count;");
            Writer.WriteLine("\tReflectionArgument* args;");
            Writer.WriteLine("} ReflectionMember;\n");

            Writer.WriteLine("typedef struct ReflectionType {");
            Writer.WriteLine("\tconst char* name;");
            Writer.WriteLine("\tint count;");
            Writer.WriteLine("\tReflectionMember* children;");
            Writer.WriteLine("} ReflectionType;\n");

            Writer.WriteLine("static ReflectionMember* ReflectionType_getMember_string(ReflectionType* this, char* name) {");
            Writer.WriteLine("\tfor (int i = 0; i < this->count; i++) {");
            Writer.WriteLine("\t\tif (strcmp(this->children[i].name, name) == 0) {");
            Writer.WriteLine("\t\t\treturn &this->children[i];");
            Writer.WriteLine("\t\t}");
            Writer.WriteLine("\t}");
            Writer.WriteLine("\treturn NULL;");
            Writer.WriteLine("}\n");

            Writer.WriteLine("static void* ReflectionType_getFunction_string(ReflectionType* this, char* name) {");
            Writer.WriteLine("\tfor (int i = 0; i < this->count; i++) {");
            Writer.WriteLine("\t\tReflectionMember member = this->children[i];");
            Writer.WriteLine("\t\tif (member.function!=NULL && strcmp(member.name, name) == 0) {");
            Writer.WriteLine("\t\t\treturn member.function;");
            Writer.WriteLine("\t\t}");
            Writer.WriteLine("\t}");
            Writer.WriteLine("\treturn NULL;");
            Writer.WriteLine("}\n");

            Writer.Write("static const ReflectionType* __TypesMap__[");
            Writer.Write(Class.CounterID);
            Writer.WriteLine("] = {");
            foreach (var cls in Builder.Classes.Values.OrderBy(c => c.ID)) {
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) continue;
                Writer.Write("\t&(ReflectionType){\"");
                Writer.Write(cls.Token.Value);
                Writer.Write("\", ");
                var children = cls.Children.Where(c => c.Access != AccessType.STATIC && /*c.HasGenerics == false &&*/ ((c is Var v && v.Usage > 0) || (c is Function f && f.Usage > 0))).ToList();
                Writer.Write(children.Count);
                if (children.Count > 0) {
                    Writer.Write(",\n\t\t(ReflectionMember[");
                    Writer.Write(children.Count);
                    Writer.Write("]) {");
                    bool arg = false;
                    bool started = false;
                    foreach (var child in children) {
                        //if (child.HasGenerics) continue;
                        if (child.Access == AccessType.STATIC) continue;
                        switch (child) {
                            case Var v when v.Usage == 0:
                            case Function f when f.Usage == 0:
                                continue;
                        }
                        if (started)
                            Writer.Write(", ");
                        started = true;
                        Writer.Write("\n\t\t\t(ReflectionMember){\"");
                        Writer.Write(child.Token.Value);
                        Writer.Write("\", ");
                        if (child is Var && child is not Property) {
                            Writer.Write("offsetof(");
                            Writer.Write(cls.Token.Value);
                            Writer.Write(", ");
                            Writer.Write(child.Token.Value);
                            Writer.Write("), ");
                        } else {
                            Writer.Write("0, ");
                        }
                        arg = false;
                        switch (child) {
                            case Property p:
                                Writer.Write("4, ");
                                Writer.Write(p.Type.ID);
                                Writer.Write(", 0, NULL, 0, NULL");
                                break;
                            case Field v:
                                Writer.Write("1, ");
                                Writer.Write(v.Type.ID);
                                Writer.Write(", ");
                                Writer.Write(v.Arrays?.Count ?? 0);
                                Writer.Write(", NULL, 0, NULL");
                                break;
                            case Function f:
                                Writer.Write(f is Constructor ? "3" : "2");
                                Writer.Write(", ");
                                Writer.Write(f.Type?.ID ?? 0);
                                Writer.Write(", ");
                                Writer.Write(f.TypeArray ? 1 : 0);
                                Writer.Write(", ");
                                Writer.Write(f is Constructor ctor && ctor.Type.Access == AccessType.STATIC ? "NULL" : f.Real);
                                Writer.Write(", ");
                                if (f.Parameters != null) {
                                    arg = true;
                                    Writer.Write(f.Parameters?.Children?.Count ?? 0);
                                    Writer.Write(",\n\t\t\t\t(ReflectionArgument[");
                                    Writer.Write(f.Parameters.Children.Count);
                                    Writer.Write("]) {");
                                    for (int p = 0; p < f.Parameters.Children.Count; p++) {
                                        var param = f.Parameters.Children[p] as Parameter;
                                        if (p > 0) Writer.Write(", ");
                                        Writer.Write("\n\t\t\t\t\t(ReflectionArgument){\"");
                                        Writer.Write(param.Token.Value);
                                        Writer.Write("\", ");
                                        Writer.Write(param.Type.ID);
                                        Writer.Write(", ");
                                        Writer.Write(param.Arrays?.Count ?? 0);
                                        Writer.Write("}");
                                    }
                                    Writer.Write("\n\t\t\t\t},");
                                } else {
                                    Writer.Write("0, NULL");
                                }
                                break;
                        }
                        if (arg) {
                            Writer.Write("\n\t\t\t");
                        }
                        Writer.Write("}");
                    }
                    Writer.WriteLine("\n\t\t}\n\t},");
                } else {
                    Writer.WriteLine(", NULL},");
                }
            }
            Writer.WriteLine("\t0\n};\n\n");

            Writer.WriteLine("static ReflectionType* getType_string(char* name) {");
            Writer.WriteLine("\tfor (int i = 0; i < " + Class.CounterID + "; i++) {");
            Writer.WriteLine("\t\tReflectionType* tp = __TypesMap__[i];");
            Writer.WriteLine("\t\tif (strcmp(tp->name, name) == 0) {");
            Writer.WriteLine("\t\t\treturn tp;");
            Writer.WriteLine("\t\t}");
            Writer.WriteLine("\t}");
            Writer.WriteLine("\treturn NULL;");
            Writer.WriteLine("}\n");
        }
        void SaveChecks() {
            Writer.WriteLine("""
                void NullException(int line, char* file) {
                	fprintf(stderr, "Null Exception Value\n  File '%s':%d\n", file, line);
                	exit(-1);
                }

                """);

            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsPrimitive || cls.Access == AccessType.STATIC) continue;
                if (cls.Usage == 0) continue;
                Writer.Write(cls.Real);
                Writer.Write("* CHECK_");
                Writer.Write(cls.Token.Value);
                Writer.Write("(");
                Writer.Write(cls.Real);
                if (cls.IsPrimitive == false) {
                    Writer.Write("*");
                }
                Writer.WriteLine(" value, int line, const char* file) {");
                Writer.WriteLine("\tif(!value) NullException(line,file);");
                Writer.WriteLine("\treturn value;");
                Writer.WriteLine("}\n");
            }
        }
        void SaveDeclarations() {
            SaveClassesPrototypes();
            Writer.WriteLine();
            SaveClassesDeclarations();
            SaveClassProperties();
            SaveEnumsDeclarations();
            Writer.WriteLine();
            SaveFunctionsDeclarations();
            SaveGlobals();
        }
        void SaveImplementations() {
            SaveClassesInitializers();
            Writer.WriteLine();
            SaveFunctionsImplementations();
            Writer.WriteLine();
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
            var values = Builder.Program.Find<AST>().Where(a => a.Annotations != null);
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
        }
        private void SaveGlobals() {
            SaveGlobals(Builder.Program);
            foreach (var module in Builder.Program.Find<Module>()) {
                SaveGlobals(module);
            }
            Writer.WriteLine();
        }
        void SaveGlobals(Module module) {
            module.Children.ForEach((item) => {
                switch (item) {
                    case Var v:
                        v.Save(Writer, Builder);
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
                        v.SaveInitializer(Writer, Builder);
                        Writer.WriteLine(';');
                        break;
                }
            });
            SaveMain();
            Writer.WriteLine("}");
            Writer.WriteLine("""
                void sighandler(int signum) {
                   printf("Caught signal %d, coming out...\n", signum);
                   exit(1);
                }
                """);
            Writer.WriteLine("int main(int argc, char *argv[]) {");
            //Writer.WriteLine("\tsignal(SIGINT, sighandler);");
            //Writer.WriteLine("\tsignal(SIGTERM, sighandler);");
            //Writer.WriteLine("\tsignal(SIGSEGV, sighandler);");
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
                    Writer.Write(param.Token.Value);
                    Writer.Write("=(");
                    Writer.Write(param.Type.Token.Value);
                    Writer.Write(")\\n");
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
                    if (param.Type.IsPrimitive == false) {
                        Writer.Write('*');
                    }
                    if (param.Arrays != null) {
                        Writer.Write('*');
                    }
                    Writer.Write(' ');
                    Writer.Write(param.Token.Value);
                    Writer.WriteLine(" = 0;");
                }
                if (hasError) return;
                Writer.WriteLine("\tfor(int i=1;i<argc;i++) {");
                Writer.WriteLine("\t\tchar* arg = argv[i];");
                for (int i = 0; i < Builder.Program.Main.Parameters.Children.Count; i++) {
                    var p = Builder.Program.Main.Parameters.Children[i] as Parameter;
                    Writer.Write("\t\tif(strncmp(\"");
                    Writer.Write(p.Token.Value);
                    Writer.Write("=\", arg, ");
                    Writer.Write(p.Token.Value.Length + 1);
                    Writer.WriteLine(")==0) {");
                    Writer.Write("\t\t\t");
                    Writer.Write(p.Token.Value);
                    Writer.Write(" = ");
                    bool isString = false;
                    bool moreParams = false;
                    switch (p.Type.Token.Value) {
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
                    Writer.Write("string_substring_i32(arg,");
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
                    Writer.Write(p.Token.Value);
                }
            }
            Writer.WriteLine(");");
        }
        #region classes
        private void SaveClassesInitializers() {
            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) continue;
                Writer.Write(cls.Real);
                Writer.Write("* ");
                Writer.Write(cls.Real);
                Writer.Write("_initializer(");
                Writer.Write(cls.Real);
                Writer.WriteLine("* this);");
            }

            Writer.WriteLine();
            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) continue;
                Writer.Write(cls.Real);
                Writer.Write("* ");
                Writer.Write(cls.Real);
                Writer.Write("_initializer(");
                Writer.Write(cls.Real);
                Writer.WriteLine("* this) {");
                if (cls.IsBased) {
                    Writer.Write(cls.Base.Real);
                    Writer.WriteLine("_initializer(this);");
                }
                if (cls.Children != null) {
                    for (int i = 0; i < cls.Children.Count; i++) {
                        if (cls.Children[i] is Var v) {
                            if (v.Initializer != null) {
                                Writer.Write("\tthis->");
                                Writer.Write(v.Token.Value);
                                Writer.Write(" = ");
                                v.Initializer.Save(Writer, Builder);
                                Writer.WriteLine(";");
                            } else if (v.Arrays != null) {

                            }
                        }
                    }
                }
                Writer.WriteLine("\treturn this;");
                Writer.WriteLine("}");
            }
        }

        void SaveStaticClassMembers(Class cls) {
            if (cls.Children == null) return;
            foreach (var child in cls.Children) {
                if (child is Field f && f.Access == AccessType.STATIC) {
                    f.Real = cls.Token.Value + "_" + f.Token.Value;
                    f.Save(Writer, Builder);
                    if (f.Initializer != null) {
                        Writer.Write(" = ");
                        f.Initializer.Save(Writer, Builder);
                    } else {
                        Writer.Write(" = 0");
                    }
                    Writer.WriteLine(";");
                }
            }
        }
        void SaveClassesDeclarations() {
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    SaveStaticClassMembers(cls);
                    continue;
                }
                if (cls.Usage == 0) continue;
                Writer.WriteLine();
                Writer.Write("type ");
                Writer.Write(cls.Real);
                Writer.WriteLine(" {");
                if (cls.IsBased) {
                    Writer.Write("\t");
                    Writer.Write(cls.Base.Real);
                    Writer.WriteLine(";");
                }
                if (cls.Children != null) {
                    foreach (var child in cls.Children) {
                        switch (child) {
                            case Property property:
                                if (property.SimpleKind != 0) {
                                    Writer.Write("\t");
                                    Writer.Write(property.Type.Real);
                                    if (property.Type.IsPrimitive == false) {
                                        Writer.Write("*");
                                    }
                                    Writer.Write(" ");
                                    Writer.Write(property.Token.Value);
                                    Writer.WriteLine(";");
                                }
                                continue;
                            case Var v:
                                //if (v.HasGenerics) {
                                //    continue;
                                //}
                                Writer.Write("\t");
                                v.Save(Writer, Builder);
                                Writer.WriteLine(";");
                                break;
                        }
                    }
                }
                Writer.Write("} ");
                Writer.Write(cls.Real);
                Writer.Write(";\n");
            }
            Writer.WriteLine();
        }

        private void SaveClassProperties() {
            foreach (var cls in Builder.Classes.Values) {
                foreach (var property in cls.Find<Property>()) {
                    if (property.Usage == 0) continue;
                    if (property.SimpleKind != Property.PropertyKind.None) {
                        continue;
                    }
                    if (property.Getter != null) {
                        property.Getter.SaveDeclaration(Writer, Builder);
                        Writer.WriteLine(";");
                    }
                    if (property.Setter != null) {
                        property.Setter.SaveDeclaration(Writer, Builder);
                        Writer.WriteLine(";");
                    }
                }
            }
        }

        bool SaveClassesPrototypes() {
            bool ok = false;
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) {
                    continue;
                }
                Writer.Write("type ");
                Writer.Write(cls.Real);
                Writer.Write(" ");
                Writer.Write(cls.Real);
                Writer.WriteLine(";");
                ok = true;
            }
            return ok;
        }
        #endregion

        #region Enums

        bool SaveEnumsDeclarations() {
            bool ok = false;
            foreach (var enm in Builder.Enums.Values) {
                if (enm.IsNative) {
                    continue;
                }
                if (enm.Usage == 0) {
                    continue;
                }
                foreach (EnumMember child in enm.Children) {
                    Writer.Write(enm.Type.Real);
                    if (enm.Type.IsPrimitive == false) {
                        Writer.Write('*');
                    }
                    Writer.Write(' ');
                    child.Save(Writer, Builder);
                    Writer.Write(" = ");
                    if (child.Expression != null) {
                        child.Expression.Save(Writer, Builder);
                    } else {
                        Writer.Write(enm.Children.IndexOf(child));
                    }
                    Writer.WriteLine(";");
                    ok = true;
                }
            }
            if (ok) {
                Writer.WriteLine();
            }
            return ok;
        }

        #endregion

        #region functions

        bool SaveFunctionsDeclarations() {
            bool ok = false;
            foreach (Function func in Builder.Functions.Values) {
                if (func.IsNative || (func is Constructor ctor && ctor.Type.Access == AccessType.STATIC)) {
                    continue;
                }
                if (func.Usage == 0) {
                    var c = func as Constructor;
                    if (c != null && (c.Parent as Class).Usage > 0) {
                    } else {
                        continue;
                    }
                }
                func.SaveDeclaration(Writer, Builder);
                Writer.WriteLine(";");
                ok = true;
            }
            Writer.WriteLine();
            return ok;
        }

        void SaveFunctionsImplementations() {
            foreach (Function func in Builder.Functions.Values) {
                if (func.IsNative || (func is Constructor ctor && ctor.Type.Access == AccessType.STATIC)) {
                    continue;
                }
                //if (func.Usage == 0) continue;
                if (func.Usage == 0) {
                    var c = func as Constructor;
                    if (c != null && (c.Parent as Class).Usage > 0) {
                    } else {
                        continue;
                    }
                }
                func.Save(Writer, Builder);
            }
        }

        #endregion
    }
}