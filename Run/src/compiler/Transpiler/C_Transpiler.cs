﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Run {
    public class C_Transpiler : Transpiler {
        public C_Transpiler(Builder builder) : base(builder) { }

        public override void Save(string path) {
            if (string.IsNullOrEmpty(Destination) == false) return;
            if (string.IsNullOrEmpty(path)) {
                throw new Exception("Path can't be null or empty");
            }

            Destination = path + ".c";
            using var stream = new FileStream(Destination, FileMode.Create);
            Save(stream);
        }

        public override void Save(Stream stream) {
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
                #include <stdarg.h>
                #include <stdbool.h>
                """);
            SaveAnnotations();
            Writer.WriteLine("""

                int* mapAlloc;
                int mapSize = 0;
                int mapCapacity = 16;

                void resizeMap() {
                    if(mapSize == 0) {
                        mapAlloc = (int*)malloc(mapCapacity * sizeof(int));
                    } else if (mapSize >= mapCapacity) {
                        mapCapacity *= 2;
                        mapAlloc = (int*)realloc(mapAlloc, mapCapacity * sizeof(int));
                    }
                }

                #define NEW(T,total,id) (T*)Alloc(sizeof(T)*total, id)
                #define REGISTER(val,id) mapAlloc[mapSize++] = (int)val; mapAlloc[mapSize++] = id; val
                #define REGISTER_VAR(var,val,id) mapAlloc[mapSize++] = (int)(var); mapAlloc[mapSize++] = id; var = val

                void* Alloc(int size, int id) {
                  void* mem = malloc(size);
                  resizeMap();
                  return REGISTER(mem, id);
                }

                #define SCOPE(T)  &((T){0})
                #define DELETE(V) free(V); V = 0
                #define CAST(T,exp) (*(T*)exp)
                #define SIZEOF(V) (int)((char *)(&V+1)-(char*)(&V))
                #define CONVERT(T,ptr) *((T*)ptr)

                bool IS(int *a, int b) {
                  for (int i = 0; i < mapSize; i++) {
                    if (mapAlloc[i] == (int)a && mapAlloc[i + 1] == b) {
                      return true;
                    }
                  }
                  return false;
                }

                #define type typedef struct
                #define null NULL

                #define TRY(j) do { jmp_buf j; switch (setjmp(j)) { case 0: 
                #define CATCH(x) break; case x:
                #define ENDTRY } } while (0)
                #define THROW(j,x) longjmp(j, x)

                type ReflectionArgument ReflectionArgument;
                type ReflectionMember ReflectionMember;
                type ReflectionType ReflectionType;

                """);
            SaveDeclarations();
            //SaveReflectionDeclarations();
            //SaveChecks();
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
            var tcc = Directory.EnumerateFiles(location, "tcc.exe", SearchOption.AllDirectories).FirstOrDefault();
            var info = new ProcessStartInfo(tcc) {
                Arguments = "-I" + Path.GetDirectoryName(location) + "/lib " + (Builder.Program.HasMain ? "-o " : "-c ") + "..\\" + Builder.Program.Token.Value + ".exe " + Destination + " -w " + (libraries.Count > 0 ? " -L" + string.Join(" -L", libraries.Select(Path.GetDirectoryName)) + "\"" + " -l\"" + string.Join(" -l\"", libraries.Select(Path.GetFileName)) : ""),
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var proc = Process.Start(info);
            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                return false;
            }
            return true;
        }
        private void SaveReflectionDeclarations() {

            Writer.WriteLine("type ReflectionArgument {");
            Writer.WriteLine("\tconst char* name;");
            Writer.WriteLine("\tint id;");
            Writer.WriteLine("\tint array;");
            Writer.WriteLine("} ReflectionArgument;\n");

            Writer.WriteLine("""
                type ReflectionMember {
                    const char* name;
                    int offset;
                    int kind;
                    int id;
                    int array;
                    void* function;
                    int count;
                    ReflectionArgument* args;
                } ReflectionMember;

                type ReflectionType {
                    const char* name;
                    int id;
                    int based;
                    int count;
                    ReflectionMember* children;
                } ReflectionType;

                static ReflectionMember* ReflectionType_getMember_string(ReflectionType* this, char* name) {
                    for (int i = 0; i < this->count; i++) {
                        if (strcmp(this->children[i].name, name) == 0) {
                            return &this->children[i];
                        }
                    }
                    return NULL;
                }

                static void* ReflectionType_getFunction_string(ReflectionType* this, char* name) {
                    for (int i = 0; i < this->count; i++) {
                        ReflectionMember member = this->children[i];
                        if (member.function!=NULL && strcmp(member.name, name) == 0) {
                            return member.function;
                        }
                    }
                    return NULL;
                }

                #define REFLETION_FIELD 1
                #define REFLETION_FUNCTION 2
                #define REFLETION_CONSTRUCTOR 3
                #define REFLETION_PROPERTY 4
                #define REFLETION_INDEXER 5
                """);

            foreach (var cls in Builder.Classes.Values.OrderBy(c => c.ID)) {
                if (cls.IsNative) continue;
                Writer.Write("ReflectionType reflection");
                Writer.Write(cls.Real);
                Writer.WriteLine(" = {");
                Writer.Write("\t\"");
                Writer.Write(cls.Token.Value);
                Writer.Write("\", ");
                Writer.Write(cls.ID);
                Writer.Write(", ");
                Writer.Write(cls.Base?.ID ?? -1);
                Writer.Write(", ");
                if (cls.Access == AccessType.STATIC || cls.Usage == 0) {
                    Writer.WriteLine("0, NULL\n};");
                    continue;
                }
                SaveClassReflection(cls);
            }
            Writer.Write("\nstatic const ReflectionType* __TypesMap__[");
            Writer.Write(Class.CounterID);
            Writer.WriteLine("] = {");
            foreach (var cls in Builder.Classes.Values.OrderBy(c => c.ID)) {
                if (cls.IsNative) continue;
                Writer.Write("\t&reflection");
                Writer.Write(cls.Real);
                Writer.WriteLine(", ");
            }
            Writer.WriteLine("\t0\n};\n\n");

            Writer.WriteLine("""
                static ReflectionType* getType_string(char* name) {
                    for (int i = 0; i < 
                """ + Class.CounterID + """
                ; i++) {
                        ReflectionType* tp = __TypesMap__[i];
                        if (strcmp(tp->name, name) == 0) {
                            return tp;
                        }
                    }
                    return NULL;
                }
                """);
        }
        void SaveClassReflection(Class cls) {
            var children = cls.Children.Where(c => c.Access != AccessType.STATIC && (c is Var v && v.Usage > 0 || c is Function f && f.Usage > 0)).ToList();
            Writer.Write(children.Count);
            if (children.Count > 0) {
                Writer.Write(",\n\t(ReflectionMember[");
                Writer.Write(children.Count);
                Writer.Write("]) {");
                bool started = false;
                foreach (var child in children) {
                    if (child.Access == AccessType.STATIC) continue;
                    switch (child) {
                        case Var v when v.Usage == 0:
                        case Function f when f.Usage == 0:
                            continue;
                    }
                    if (started)
                        Writer.Write(", ");
                    started = true;
                    SaveReflectionMember(child, cls);
                }
                Writer.WriteLine("\n\t}\n};");
            } else {
                Writer.WriteLine(", NULL\n};");
            }
        }
        void SaveReflectionMember(AST child, Class cls) {
            Writer.WriteLine("\n\t\t(ReflectionMember){");
            Writer.Write("\t\t\t\"");
            Writer.Write(child.Token.Value);
            Writer.Write("\", ");
            if (child is Var && child is not GetterSetter) {
                Writer.Write("offsetof(");
                Writer.Write(cls.Real);
                Writer.Write(", ");
                Writer.Write(child.Real);
                Writer.Write("), ");
            } else {
                Writer.Write("0, ");
            }
            var arg = false;
            switch (child) {
                case Property p:
                    Writer.Write("REFLETION_PROPERTY, ");
                    Writer.Write(p.Type.ID);
                    Writer.Write(", 0, NULL, 0, NULL");
                    break;
                case Indexer x:
                    Writer.Write("REFLETION_INDEXER, ");
                    Writer.Write(x.Type.ID);
                    Writer.Write(", ");
                    Writer.Write(x.Index.Type.ID);
                    Writer.Write(", NULL, 0, NULL\n\t\t");
                    break;
                case Field v:
                    Writer.Write("REFLETION_FIELD, ");
                    Writer.Write(v.Type.ID);
                    Writer.Write(", ");
                    Writer.Write(v.Arguments?.Count ?? 0);
                    Writer.Write(", NULL, 0, NULL\n\t\t");
                    break;
                case Function f:
                    Writer.Write(f is Constructor ? "REFLETION_CONSTRUCTOR" : "REFLETION_FUNCTION");
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
                        Writer.Write(",\n\t\t\t(ReflectionArgument[");
                        Writer.Write(f.Parameters.Children.Count);
                        Writer.Write("]) {");
                        for (int p = 0; p < f.Parameters.Children.Count; p++) {
                            var param = f.Parameters.Children[p] as Parameter;
                            if (p > 0) Writer.Write(", ");
                            Writer.Write("\n\t\t\t\t(ReflectionArgument){\"");
                            Writer.Write(param.Token.Value);
                            Writer.Write("\", ");
                            Writer.Write(param.Type.ID);
                            Writer.Write(", ");
                            Writer.Write(param.Arguments?.Count ?? 0);
                            Writer.Write("}");
                        }
                        Writer.Write("\n\t\t\t},");
                    } else {
                        Writer.Write("0, NULL\n\t\t");
                    }
                    break;
            }
            if (arg) {
                Writer.Write("\n\t\t");
            }
            Writer.Write("}");
        }
        void SaveChecks() {
            Writer.WriteLine("""
                void NullException(int line, char* file) {
                	fprintf(stderr, "Null Exception Value\n  File '%s':%d\n", file, line);
                	exit(-1);
                }

                """);

            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsEnum) continue;
                if (/*cls.IsPrimitive || */cls.Access == AccessType.STATIC) continue;
                if (cls.Usage == 0) continue;
                Writer.Write(cls.Real);
                Writer.Write("* CHECK_");
                Writer.Write(cls.Token.Value);
                Writer.Write("(");
                Writer.Write(cls.Real);
                Writer.Write("*");
                Writer.WriteLine(" value, int line, const char* file) {");
                Writer.WriteLine("\tif(!value) NullException(line,file);");
                Writer.WriteLine("\treturn value;");
                Writer.WriteLine("}\n");
            }
        }
        void SaveDeclarations() {
            SaveClassesPrototypes();
            SaveEnumsPrototypes();
            SaveClassesDeclarations();
            SaveEnumsDeclarations();
            SaveFunctionsPrototypes();
            SaveGlobals();
        }
        void SaveImplementations() {
            SaveClassesInitializers();
            SaveClassProperties();
            SaveFunctionsImplementations();
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
                        Save(v.Initializer);
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
            Writer.WriteLine("\trun_initializer(argc, argv);");
            Writer.WriteLine("\treturn 0;");
            Writer.WriteLine("}");
            Writer.WriteLine();
        }
        void SaveMain() {
            Writer.WriteLine("setStaticClassMembersValues();");
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
                    if (param.Type.IsPrimitive == false && param.Type != Builder.CharSequence) {
                        Builder.Program.AddError(param.Token, Error.InvalidExpression);
                        hasError = true;
                    }
                    Writer.Write("\t");
                    Writer.Write(param.Type.Real);
                    Writer.Write('*');
                    if (param.Arguments != null) {
                        Writer.Write('*');
                    }
                    Writer.Write(' ');
                    Writer.Write(param.Real);
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
                    Writer.WriteLine("\t\t\tint len = strlen(arg);");
                    //Writer.Write("\t\t\t");
                    //Writer.Write(p.Real);
                    //Writer.Write(" = ");
                    bool isString = false;
                    bool moreParams = false;
                    switch (p.Type.Real) {
                        case "string":
                        case "chars":
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
                    Writer.WriteLine("int __len = strlen(arg);");
                    Writer.Write(p.Real);
                    Writer.Write(" = (char*) malloc(len-");
                    Writer.Write(p.Token.Value.Length + 1);
                    Writer.WriteLine(");");
                    Writer.Write("strncpy(");
                    Writer.Write(p.Real);
                    Writer.Write(", arg + ");
                    Writer.Write(p.Token.Value.Length + 1);
                    Writer.Write(", len - ");
                    Writer.Write(p.Token.Value.Length + 1);
                    Writer.WriteLine(");");
                    Writer.Write(p.Real);
                    Writer.Write("[len - ");
                    Writer.Write(p.Token.Value.Length + 1);
                    Writer.WriteLine("] = 0;");
                    if (moreParams) {
                        //Writer.WriteLine(",0x0,10");
                    }
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
                    Writer.Write(p.Real);
                }
            }
            Writer.WriteLine(");");
        }
        #region classes
        private void SaveClassesInitializers() {
            foreach (var cls in Builder.Classes.Values) {
                if (cls.IsEnum) continue;
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
                if (cls.IsEnum) continue;
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
                        if (cls.Children[i] is Var v && v.Access != AccessType.STATIC) {
                            if (v.Initializer != null) {
                                Writer.Write("\tthis->");
                                Writer.Write(v.Real);
                                Writer.Write(" = ");
                                Save(v.Initializer);
                                Writer.WriteLine(";");
                            } else if (v.Arguments != null) {

                            }
                        }
                    }
                }
                Writer.WriteLine("\treturn this;");
                Writer.WriteLine("}");
            }
        }

        void SaveStaticClassMembersValues(Class cls) {
            if (cls.Children == null) return;
            foreach (var child in cls.Children) {
                if (child is Field f && f.Access == AccessType.STATIC) {
                    SaveRegisterVar(f, "0");
                    Writer.WriteLine(";");
                }
            }
        }
        void SaveStaticClassMembersPrototypes(Class cls) {
            if (cls.Children == null) return;
            foreach (var child in cls.Children) {
                if (child is Field f && f.Access == AccessType.STATIC) {
                    f.Real = cls.Token.Value + f.Real;
                    Save(f, false);
                    Writer.WriteLine(";");
                    //if (f.Initializer != null) {
                    //    SaveRegisterVar(f);
                    //} else {
                    //    SaveRegisterVar(f, "0");
                    //}
                }
            }
        }
        void SaveClassesDeclarations() {
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                SaveStaticClassMembersPrototypes(cls);
                if (cls.IsEnum || cls.IsPrimitive == false) continue;
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) continue;
                SaveClassDeclaration(cls);
            }
            foreach (var cls in Builder.Classes.Values.OrderBy(e => e.BaseCount)) {
                if (cls.IsEnum || cls.IsPrimitive) continue;
                if (cls.IsNative || cls.Access == AccessType.STATIC) {
                    continue;
                }
                if (cls.Usage == 0) continue;
                SaveClassDeclaration(cls);
            }
            Writer.WriteLine();

            Writer.WriteLine("void setStaticClassMembersValues() {");
            foreach (var cls in Builder.Classes.Values) {
                SaveStaticClassMembersValues(cls);
            }
            Writer.WriteLine("}");
        }
        void SaveClassDeclaration(Class cls) {
            Writer.WriteLine();
            Writer.Write("type ");
            Writer.Write(cls.Real);
            Writer.WriteLine(" {");
            if (cls.IsBased) {
                Writer.Write(cls.Base.Real);
                Writer.WriteLine(";");
            }
            if (cls.IsPrimitive) {
                if (cls.NativeName != null) {
                    Writer.Write("\t");
                    Writer.Write(cls.NativeName);
                    Writer.WriteLine(" value;");
                }
            } else if (cls.Children != null) {
                foreach (var child in cls.Children) {
                    switch (child) {
                        case GetterSetter property:
                            if (property.SimpleKind != 0) {
                                Writer.Write("\t");
                                Writer.Write(property.Type.Real);
                                if (property.Type.IsPrimitive == false) {
                                    Writer.Write("*");
                                }
                                Writer.Write(" ");
                                Writer.Write(property.Real);
                                Writer.WriteLine(";");
                            }
                            continue;
                        case Var v:
                            if (v.Access == AccessType.STATIC) {
                                continue;
                            }
                            Writer.Write("\t");
                            Save(v);
                            Writer.WriteLine(";");
                            break;
                    }
                }
            }
            Writer.Write("} ");
            Writer.Write(cls.Real);
            Writer.Write(";\n");

            if (cls.Children != null) {
                foreach (var child in cls.Children) {
                    if (child is Var v && v.Access == AccessType.STATIC) {
                        v.Real = cls.Real + v.Token.Value;
                        Save(v);
                        Writer.WriteLine(";");
                    }
                }
            }
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
                if (cls.IsNative || cls.Access == AccessType.STATIC) continue;
                if (cls.IsEnum) continue;
                if (cls.Usage == 0) continue;
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
        bool SaveEnumsPrototypes() {
            bool ok = false;
            Writer.WriteLine();
            foreach (var enm in Builder.Classes.Values) {
                if (enm.IsEnum == false /*|| enm.IsNative*/) {
                    continue;
                }
                if (enm.Usage == 0) {
                    continue;
                }
                Writer.Write("#define ");
                Writer.Write(enm.Real);
                Writer.Write(" ");
                Writer.WriteLine(enm.Base.Real);
                ok = true;
            }
            if (ok) {
                Writer.WriteLine();
            }
            return ok;
        }
        bool SaveEnumsDeclarations() {
            bool ok = false;
            foreach (var enm in Builder.Classes.Values) {
                if (enm.IsEnum == false/* || enm.IsNative*/) {
                    continue;
                }
                if (enm.Usage == 0) {
                    continue;
                }
                foreach (EnumMember child in enm.Children) {
                    Save(child);
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
        bool SaveFunctionsPrototypes() {
            bool ok = false;
            foreach (Function func in Builder.Functions.Values) {
                if (func.IsNative || (func is Constructor ctor && ctor.Type.Access == AccessType.STATIC)) {
                    continue;
                }
                if (func.Usage == 0) {
                    if (func.Parent is Class cls) {
                        if (cls.Usage == 0 || cls.IsNative) {
                            continue;
                        }
                    } else {
                        continue;
                    }
                }
                SaveDeclaration(func);
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
                if (func.Usage == 0) {
                    if (func.Parent is Class cls) {
                        if (cls.Usage == 0 || cls.IsNative) {
                            continue;
                        }
                    } else {
                        continue;
                    }
                }
                if (func.Parent is GetterSetter) continue;
                Save(func);
            }
        }
        #endregion


        void Save(IndexerExpression exp) {
            Save(exp.Left);
            Writer.Write('[');
            Save(exp.Right);
            Writer.Write(']');
        }

        void Save(DotExpression exp) {
            Save(exp.Left);
            Writer.Write(exp.Left.Type?.IsEnum ?? false ? "_" : "->");
            Save(exp.Right);
        }

        void Save(BinaryExpression exp) {
            Save(exp.Left);
            Writer.Write(exp.Token.Value);
            Save(exp.Right);
        }

        void Save(AssignExpression exp) {
            if (exp.Right is LiteralExpression && Builder.Program.Implicits.TryGetValue(exp.Right.Type.Token.Value, out var ast)) {
                if (SaveImplicit(exp, ast)) return;
            }
            Save(exp.Left);
            Writer.Write(exp.Token.Value);
            Save(exp.Right);
        }

        void Save(UnaryExpression exp) {
            if (exp.AtRight == false) Writer.Write(exp.Token.Value);
            Save(exp.Content);
            if (exp.AtRight) Writer.Write(exp.Token.Value);
        }

        void Save(EnumMember exp) {
            Writer.Write(exp.Parent.Token.Value);
            Writer.Write('_');
            Writer.Write(exp.Token.Value);
        }

        void Save(LiteralExpression exp) {
            Writer.Write(exp.Token?.Value);
        }

        bool SaveImplicit(AssignExpression exp, AST ast) {
            switch (ast) {
                case Constructor ctor:
                    if (exp.Parent == ctor) return false;
                    SaveReturnType(ctor);
                    Save(exp.Left);
                    Writer.Write(" = ");
                    Writer.Write(ctor.Real);
                    Writer.Write("(NEW(");
                    Writer.Write(ctor.Type.Real);
                    Writer.Write(",1,");
                    Writer.Write(ctor.Type.ID);
                    Writer.Write("),");
                    Save(exp.Right);
                    Writer.Write(')');
                    return true;
            }
            return false;
        }

        void Save(ParentesesExpression exp) {
            Writer.Write('(');
            Save(exp.Content);
            Writer.Write(')');
        }

        void Save(Switch exp) {
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
            Save(exp.Condition);
            Writer.Write(" ? ");
            Save(exp.True);
            Writer.Write(" : ");
            Save(exp.False);
        }

        void Save(ThisExpression t) {
            Writer.Write("this");
        }

        void SaveBlock(Block block) {
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
                case AssignExpression a: Save(a); break;
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

        Class SaveReturnType(Function exp) {
            var cls = exp.Parent as Class;
            if (cls == null && exp.Parent is GetterSetter p) {
                cls = p.Parent as Class;
            }
            if (exp is Constructor) {
                Writer.Write(cls.Real);
                if (cls.IsPrimitive == false)
                    Writer.Write("*");
            } else {
                SaveType(exp.Type);
            }
            if (exp.TypeArray) {
                Writer.Write("*");
            }
            return cls;
        }

        void SaveDeclaration(Function exp) {
            var cls = SaveReturnType(exp);
            Writer.Write(" ");
            Writer.Write(exp.Real);
            Writer.Write("(");
            bool started = false;
            if (cls != null && exp.Access == AccessType.INSTANCE) {
                Writer.Write(cls.Real);
                if (cls.IsPrimitive == false) {
                    Writer.Write("*");
                }
                Writer.Write(" this");
                started = true;
            }
            if (exp.Parameters != null) {
                foreach (var item in exp.Parameters.Children) {
                    if (item is Parameter p) {
                        if (started) {
                            Writer.Write(", ");
                        }
                        Save(p);
                        started = true;
                    }
                }
            }
            Writer.Write(")");
        }

        void SaveMapPosition(AST ast) {
            if (ast == null || ast.Token == null) return;
            Writer.Write("int __mapPosition");
            Writer.Write(ast.Token.Position);
            Writer.WriteLine(" = mapSize;");
        }

        void RestoreMapPosition(AST ast) {
            if (ast == null || ast.Token == null) return;
            Writer.Write("mapSize = __mapPosition");
            Writer.Write(ast.Token.Position);
            Writer.WriteLine(";");
        }

        void Save(Function exp) {
            if (exp.IsNative) {
                return;
            }
            SaveDeclaration(exp);
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
                        Writer.Write(cls.Base.Real);
                        Writer.WriteLine("_this(this);");
                    }
                }
                Writer.Write(cls.Real);
                Writer.WriteLine("_initializer(this);");
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
                    //ast.Save(writer, builder);
                    Save(ast);
                } else if (exp.Parameters != null && exp.Children.Count > 1 && exp.Children[1] is AST a) {
                    //a.Save(writer, builder);
                    Save(a);
                }
                Writer.WriteLine(";\n}");
                return;
            }
            if (exp.Type != null && exp is not Constructor) {
                SaveReturnType(exp);
                Writer.WriteLine(" __RETURN__ = 0;");
            }
            SaveMapPosition(exp);
            SaveBlock(exp);
            RestoreMapPosition(exp);
            if (exp is Constructor) {
                Writer.WriteLine("return this;");
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
            if (exp.Parameters == null) return;
            foreach (var child in exp.Parameters.Children) {
                if (child is Parameter param && param.IsMember) {
                    Writer.Write("this->");
                    Writer.Write(param.Token.Value);
                    Writer.Write(" = ");
                    Writer.Write(param.Token.Value);
                    Writer.WriteLine(";");
                }
            }
        }

        void Save(Indexer exp) {
            Save(exp.Getter);
            Save(exp.Setter);
        }

        void Save(Parameter exp) {
            if (exp.IsVariadic) {
                Writer.Write("_i32 len_");
                Writer.Write(exp.Token.Value);
                Writer.Write(", ...");
                return;
            }
            //base.Save(writer, builder);
            Save(exp as Var);
            if (exp.Arguments != null) {
                //writer.Write(", int ");
                //writer.Write(Token.Value);
                //writer.Write("_size");
            }
        }

        void Save(Property exp) {
            if (exp.Initializer != null) {
                Save(exp as Var);
                return;
            }
            Save(exp.Getter);
            Save(exp.Setter);
        }

        void Save(PropertySetter exp) {
            Writer.Write(exp.Real);
            Writer.Write('(');
            Save(exp.This);
            Writer.Write(')');
        }

        void Save(Var exp, bool saveInitializer = true) {
            if (exp is Parameter p && p.Constraints != null) {
                if (p.IsVariadic) {
                    Writer.Write("int len");
                    Writer.Write(p.Real);
                    Writer.Write(", ...");
                    return;
                } else {
                    Writer.Write("void* ");
                }
            } else {
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
                if (exp.TypeArray) Writer.Write("*");
                if (exp.Type.IsPrimitive == false) {
                    Writer.Write('*');
                }
            }
            Writer.Write(exp.Real ?? exp.Token.Value);
            if (saveInitializer && exp.FindParent<Function>() != null) {
                SaveInitializer(exp, exp.TypeArray);
            }
        }
        void SaveInitializer(Var exp, bool array = true) {
            if (array && exp.Arguments != null) {
                Writer.Write('[');
                if (exp.Arguments.Count > 0) {
                    Save(exp.Arguments[0]);
                }
                Writer.Write(']');
            } else if (exp.Initializer != null && exp.Parent is not Class) {
                Writer.WriteLine(";");
                SaveRegisterVar(exp);
            }
        }

        void SaveRegisterVar(Var exp, string value = null) {
            Writer.Write("REGISTER_VAR(");
            Writer.Write(exp.Real);
            Writer.Write(", ");
            if (exp.Initializer != null) {
                Save(exp.Initializer);
            } else if (value != null) {
                Writer.Write(value);
            } else {
                Writer.Write("0");
            }
            Writer.Write(", ");
            Writer.Write(exp.Type.ID);
            Writer.Write(")");
        }

        void Save(Label exp) {
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(':');
        }

        void Save(Break exp) {
            Writer.WriteLine("break;");
        }

        void Save(Continue exp) {
            Writer.WriteLine("continue;");
        }

        void Save(Else exp) {
            Writer.Write("else ");
            if (exp.Condition != null) {

                //base.Save(writer, builder);
            } else {
                Writer.WriteLine('{');
                //SaveBlock(writer, builder);
                SaveBlock(exp);
                Writer.WriteLine('}');
            }
        }

        void Save(Scope exp) {
            Writer.Write("SCOPE(");
            Writer.Write(exp.Token.Value);
            Writer.Write(")");
        }

        void Save(TypeOf exp) {
            if (exp.Type != null && exp.Type.ID >= 0 && exp.Type.ID < Class.CounterID) {
                Writer.Write("__TypesMap__[");
                Writer.Write(exp.Type.ID);
                Writer.Write("]");
                return;
            }
            Builder.Program.AddError(exp.Token, Error.InvalidExpression);
        }

        void Save(SizeOf exp) {
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

        void SaveWhile(For exp) {
            Writer.Write("while(");
            Save(exp.Start);
        }

        void SaveBegin(For exp) {
            var var = exp.Start as Var;
            Writer.Write("for(");
            Save(var);
            Writer.Write(";;");
            Writer.Write(var.Real);
            Writer.Write("++");
        }

        void SaveIterator(For exp) {
            var var = exp.Start as Var;
            var iterator = exp.Condition as Iterator;
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
        }

        void SaveRanged(For exp) {
            var var = exp.Start as Var;
            Writer.Write("for(int ");
            Writer.Write(var.Real);
            Writer.Write(" = 0; ");
            Writer.Write(var.Real);
            Writer.Write(" < ");
            Save(exp.Condition);
            Writer.Write("; ");
            Writer.Write(var.Real);
            Writer.Write("++");
        }

        void SaveStartRanged(For exp) {
            var range = exp.Start as RangeExpression;
            Writer.Write("for(int range_");
            Writer.Write(range.Left.Token.Position);
            Writer.Write(" = ");
            Save(range.Left);
            Writer.Write("; range_");
            Writer.Write(range.Left.Token.Position);
            Writer.Write(" < ");
            Save(range.Right);
            Writer.Write("; range_");
            Writer.Write(range.Left.Token.Position);
            Writer.Write("++");
        }

        void SaveDefaultFor(For exp) {
            Writer.Write("for(");
            Save(exp.Start);
            Writer.Write(';');
            Save(exp.Condition);
            Writer.Write(';');
            Save(exp.Step);
        }

        private void SaveVarRanged(For exp) {
            var var = exp.Start as Var;
            var range = var.Initializer as RangeExpression;
            Writer.Write("for(int ");
            Writer.Write(var.Real);
            Writer.Write(" = ");
            Save(range.Left);
            Writer.Write(';');
            Writer.Write(var.Real);
            Writer.Write(" < ");
            Save(range.Right);
            Writer.Write(';');
            Writer.Write(var.Real);
            Writer.Write("++");
        }

        void Save(For exp) {
            SaveMapPosition(exp);
            switch (exp.Stage) {
                case -1: Writer.Write("while(1"); break;
                case 0 when exp.Start is RangeExpression: SaveStartRanged(exp); break;
                case 0 when exp.Start is Expression: SaveWhile(exp); break;
                case 0 when exp.Start is Var && exp.Condition == null: SaveBegin(exp); break;
                case 0 when exp.Start is Var && exp.Condition is Iterator: SaveIterator(exp); return;
                case 1 when exp.Start is Var var && var.Initializer is RangeExpression: SaveVarRanged(exp); break;
                case 1 when exp.HasRange && exp.Start is Var: SaveRanged(exp); break;
                default: SaveDefaultFor(exp); break;
            }
            Finish(exp);
        }
        void Finish(For exp) {
            if (exp.Children.Count > 0) {
                Writer.WriteLine(") {");
                SaveBlock(exp);
                RestoreMapPosition(exp);
                Writer.WriteLine("}");
                RestoreMapPosition(exp);
            } else {
                Writer.WriteLine(");");
            }
        }

        void Save(If exp) {
            Writer.Write("if(");
            Save(exp.Condition);
            Writer.WriteLine(") {");
            SaveMapPosition(exp);
            SaveBlock(exp);
            RestoreMapPosition(exp);
            Writer.WriteLine("}");
        }

        void Save(Goto exp) {
            Writer.Write("goto ");
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(';');
        }

        void Save(Delete exp) {
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
            if (exp.Content is ArrayCreationExpression array) {
                Writer.Write("(");
                Writer.Write(array.Type.Real);
                if (array.Type.IsPrimitive == false) {
                    Writer.Write("*");
                }
                Writer.Write("*)malloc(sizeof(");
                Writer.Write(array.Type.Real);
                if (array.Type.IsPrimitive == false) {
                    Writer.Write("*");
                }
                Writer.Write(") * ");
                Save(array.Content);
            } else if (exp.Content is ConstructorExpression ctor) {
                Save(ctor, exp.Type);
            } else {
                Debugger.Break();
            }
            Writer.Write(')');
        }

        void Save(CallExpression call, Class type) {
            Writer.Write(call.Function.Real);
            Writer.Write("(NEW(");
            Writer.Write(type.Real);
            Writer.Write(",1,");
            Writer.Write(type.ID);
            Writer.Write(")");
            foreach (var value in call.Arguments) {
                Writer.Write(',');
                Save(value);
            }
        }

        void Save(IdentifierExpression exp) {
            if (exp.From != null) {
                switch (exp.From) {
                    case Field f: {
                            if (f.Access != AccessType.STATIC && (exp.Parent is not DotExpression || exp.Parent is DotExpression dot && dot.Left == exp)) {
                                Writer.Write("this->");
                            }
                        }
                        break;
                    case GetterSetter p: {
                            if (p.Access != AccessType.STATIC && (exp.Parent is not DotExpression || exp.Parent is DotExpression dot && dot.Left == exp)) {
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
            Writer.Write("((");
            Save(exp.Left);
            Writer.Write(").reflection->id==reflection");
            Writer.Write(exp.Right.Type.Real);
            Writer.Write(".id)");
        }

        void Save(AsExpression exp) {
            if (exp.Type == null) return;
            if (exp.Type.IsPrimitive == false && exp.Type.IsNative == false) {
                Writer.Write("*");
            }
            Writer.Write("(");
            SaveType(exp.Type);
            if (exp.Type.IsPrimitive == false && exp.Type.IsNative == false) {
                Writer.Write("*");
            }
            if (exp.IsArray) {
                Writer.Write("*");
            }
            Writer.Write(")");
            Save(exp.Left);
        }

        void SaveNative(CallExpression exp) {
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
                    var temp = Writer;
                    Writer = memory;
                    Save(value);
                    Writer = temp;
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

        void Save(CallExpression exp) {
            if (exp.Function.IsNative && exp.Function.NativeNames?.Count >= 2) {
                SaveNative(exp);
                return;
            }
            Writer.Write(exp.Function?.Real ?? exp.Real ?? exp.Token.Value);
            Writer.Write('(');
            if (exp.Caller != null && exp.Function.Access != AccessType.STATIC) {
                Save(exp.Caller);
                if (exp.Arguments.Count > 0) Writer.Write(", ");
            }
            for (int i = 0; i < exp.Arguments.Count; i++) {
                if (exp.Function.HasVariadic && i == exp.Function.Parameters.Children.Count - 1) {
                    Writer.Write(exp.Arguments.Count - exp.Function.Parameters.Children.Count + 1);
                    Writer.Write(", ");
                }
                Save(exp.Arguments[i]);
                if (i < exp.Arguments.Count - 1) {
                    Writer.Write(", ");
                }
            }
            Writer.Write(')');
        }

        void Save(Default exp) {
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
            Writer.Write("__DEFER_STAGE__");
            Writer.Write(exp.ID);
            Writer.Write(" = ");
            Writer.Write(exp.Token.Value);
            Writer.WriteLine(";");
        }
    }
}