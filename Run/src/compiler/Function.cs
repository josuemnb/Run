using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Run.V12 {
    public class Parameter : Var {
        public bool IsMember;
        public bool IsVariadic;
        public List<AST> Constraints;

        public override void Save(TextWriter writer, Builder builder) {
            if (IsVariadic) {
                writer.Write("int len_");
                writer.Write(Token.Value);
                writer.Write(", ...");
                return;
            }
            base.Save(writer, builder);
            if (Arrays != null) {
                //writer.Write(", int ");
                //writer.Write(Token.Value);
                //writer.Write("_size");
            }
        }
    }

    public class Main : Function {
        public override void Parse() {
            Program.Main = this;
            Program.HasMain = true;
            Token = Scanner.Current.Clone();
            Real = "run_main";
            if (Scanner.Expect('(') && Scanner.Expect(')') == false) {
                ParseParameters();
            }
            ParseBlock();
        }
    }
    public class Constructor : Function {
        public override string ToString() {
            return "This: " + Type;
        }
    }

    public class Function : Block {
        public Block Parameters;
        public Class Type;
        public Token Pointer;
        public List<string> Native;
        public bool HasDefers;
        public bool IsArrow;
        public bool TypeArray;
        public bool HasInterface;
        public bool HasVariadic;
        public int Usage = 0;
        public bool IsAutoFree { get; internal set; }

        public override void Parse() {
            SetAccess();
            var cls = Parent as Class;
            if (cls != null && cls.Access == AccessType.STATIC && Access != AccessType.STATIC) {
                Program.AddError(Scanner.Current, Error.IncompatibleAccessClassStatic);
            }
            GetAnnotations();
            if (GetName(out Token) == false) return;
            if (Token.Value == "main") {
                (Parent as Block).Children.Remove(this);
                (Parent as Block).Add<Main>().Parse();
                return;
            }
            //if (Scanner.Expect('<')) {
            //    Generics = ParseGenerics(this);
            //    ValidateGenerics();
            //}
            if (Scanner.Expect('(') && Scanner.Expect(')') == false) {
                ParseParameters();
            }
            if (Scanner.Expect(':')) {
                GetReturnType();
            }
            if (Scanner.Expect("=>")) {
                ParseArrow();
                return;
            }
            if (Scanner.Expect('=')) {
                GetName(out Pointer);
                return;
            }
            if (IsNative) {
                if (Scanner.IsEOL() == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                }
                return;
            }
            if (cls != null && (Parameters == null || Parameters.Children.Count == 0)) {
                switch (Token.Value) {
                    case "dispose": cls.Dispose = this; break;
                    case "toString" when Type.Token.Value == "string": cls.toString = this; break;
                }
            }
            ParseBlock();
        }

        private void ValidateGenerics() {
            //var cls = Parent as Class;
            //if (cls == null || cls.HasGenerics == false) return;
            //for (int i = 0; i < Generics.Count; i++) {
            //    var gen = Generics[i];
            //    if (cls.Generics.Find(g => g.Token.Value == gen.Token.Value) is Generic generic) {
            //        Program.AddError(gen.Token, Error.GenericNameAlreadyClassDefined);
            //    }
            //}
        }

        internal void ParseArrow() {
            IsArrow = true;
            base.ParseBlock(true);
        }

        public override AST FindChildren(string name) {
            if (base.FindChildren(name) is AST a) {
                return a;
            }
            if (Parameters != null) {
                foreach (Parameter p in Parameters.Children) {
                    if (p.Token.Value == name) {
                        return p;
                    }
                }
            }
            foreach (var child in Children) {
                if (child.Token != null && child.Token.Value == name) {
                    return child;
                }
            }
            return null;
        }

        //public override AST FindName(string name) {
        //    if (Token.Value == name) {
        //        return this;
        //    }
        //    if (Parameters != null) {
        //        foreach (Parameter p in Parameters.Children) {
        //            if (p.Token.Value == name) {
        //                return p;
        //            }
        //        }
        //    }
        //    return Parent?.FindName(name);
        //}

        protected new void ParseBlock(bool once = false) {
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
            }
            base.ParseBlock(once);
        }

        protected void ParseParameters() {
            var cls = FindParent<Class>();
            Parameters = Add<Block>();
        again:
            if (HasVariadic) {
                Program.AddError(Scanner.Current, Error.VariadicParameterAlreadyDefined);
                return;
            }
            var param = Parameters.Add<Parameter>();
            if (Scanner.Expect("...")) {
                param.IsVariadic = true;
                HasVariadic = true;
            } else if (Scanner.Expect('.')) {
                if ((Parent is Class) == false) {
                    Program.AddError(Scanner.Current, Error.OnlyInClassScope);
                    return;
                }
                param.IsMember = true;
            }
            if (GetName(out param.Token) == false) {
                Scanner.SkipLine();
                return;
            }
            if (param.IsMember == false) {
                if (Scanner.Expect(':') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingAssign);
                    return;
                }
                //if (Scanner.Expect('(')) {
                //    HasInterface = true;
                //    GetConstraints(param);
                //} else {
                param.GetReturnType();
                //if (cls != null && cls.HasGenerics) {
                //    if (cls.Generics.Find(g => g.Token.Value == param.Type.Token.Value) is Generic generic) {
                //        param.Generics ??= new(1);
                //        param.Generics.Add(generic);
                //        if (Generics?.Any(g => g.Token.Value == generic.Token.Value) == false) {
                //            Generics ??= new(1);
                //            Generics.Add(generic);
                //        }
                //    }
                //}
                //}
            }
            if (Scanner.Expect(',')) goto again;
            if (Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                return;
            }
        }

        public Class SaveReturnType(TextWriter writer) {
            var cls = Parent as Class;
            if (cls == null && Parent is Property p) {
                cls = p.Parent as Class;
            }
            if (this is Constructor) {
                writer.Write(cls.Real);
                if (cls.IsPrimitive == false)
                    writer.Write("*");
            } else if (Type == null) {
                writer.Write("void");
            } else {
                writer.Write(Type.Real ?? Type.Token.Value);
                if (Type.IsPrimitive == false) {
                    writer.Write("*");
                }
            }
            if (TypeArray) {
                writer.Write("*");
            }
            return cls;
        }

        void GetConstraints(Parameter param) {
            param.Constraints = new List<AST>();
        again:
            if (GetName(out Token name) == false) {
                return;
            }
            if (Scanner.Expect('(')) {
                var interf = new Interface() {
                    Token = name,
                };
                interf.SetParent(param);
                param.Constraints.Add(interf);
                if (Scanner.Expect(')')) goto finish;
                loop:
                if (GetName(out Token type) == false) {
                    return;
                }
                interf.Add<Identifier>().Token = type;
                if (Scanner.Expect(')')) {
                    goto finish;
                }
                if (Scanner.Expect(',') == false) {
                    Program.AddError(Error.ExpectingCommaOrCloseParenteses, param);
                    return;
                }
                goto loop;
            } else {
                var id = new Identifier {
                    Token = name,
                };
                id.SetParent(param);
                param.Constraints.Add(id);
            }
        finish:
            if (Scanner.Expect(')')) {
                return;
            }
            if (Scanner.Expect(',') == false) {
                Program.AddError(Error.ExpectingCommaOrCloseParenteses, param);
                return;
            }
            goto again;
        }

        public override void Print() {
            Print(this);
            if (Annotations != null)
                foreach (var annotation in Annotations)
                    annotation.Print();
            //if (Generics != null) {
            //    foreach (var generic in Generics)
            //        generic.Print();
            //}
            foreach (var child in Children) {
                child.Print();
            }
        }
        public void GetReturnType() {
            if (GetName(out Token type) == false) return;
            Type = new Class() {
                Token = type,
            };
            Type.SetParent(this);
            if (Scanner.Expect('[')) {
                if (Scanner.Expect(']') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
                }
                TypeArray = true;
            }
        }

        public void SaveDeclaration(TextWriter writer, Builder builder) {
            var cls = SaveReturnType(writer);
            writer.Write(" ");
            writer.Write(Real);
            writer.Write("(");
            bool started = false;
            if (cls != null && Access == AccessType.INSTANCE) {
                writer.Write(cls.Real);
                if (cls.IsPrimitive == false) {
                    writer.Write("*");
                }
                writer.Write(" this");
                started = true;
            }
            if (Parameters != null) {
                foreach (var item in Parameters.Children) {
                    if (item is Parameter p) {
                        if (started) {
                            writer.Write(", ");
                        }
                        p.Save(writer, builder);
                        started = true;
                    }
                }
            }
            writer.Write(")");
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (IsNative) {
                return;
            }
            SaveDeclaration(writer, builder);
            if (Pointer != null) {
                writer.Write(" = ");
                writer.Write(Pointer.Value);
                return;
            }
            writer.WriteLine(" {");
            if (this is Constructor) {
                var cls = Parent as Class;
                if (cls.IsBased) {
                    bool found = false;
                    if (Parameters != null && Parameters.Children.Count > 0) {
                        foreach (var c in cls.Find<Constructor>()) {
                            if (c.Parameters != null && c.Parameters.Children.Count == Parameters.Children.Count) {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found == false) {
                        writer.Write(cls.Base.Real);
                        writer.WriteLine("_this(this);");
                    }
                }
            }
            if (Parameters != null) {
                foreach (Parameter param in Parameters.Children) {
                    if (param.IsMember) {
                        writer.Write("this->");
                        writer.Write(param.Token.Value);
                        writer.Write(" = ");
                        writer.Write(param.Token.Value);
                        writer.WriteLine(";");
                    }
                }
            }
            if (IsArrow && ((Children.Count > 0 && (Parameters == null || Parameters.Children.Count == 0)) || (Children.Count > 1 && (Parameters != null && Parameters.Children.Count > 0)))) {
                if (Type != null) {
                    writer.Write("return ");
                }
                if ((Parameters == null || Parameters.Children.Count == 0) && Children[0] is AST ast) {
                    ast.Save(writer, builder);
                } else if (Parameters != null && Children.Count > 1 && Children[1] is AST a) {
                    a.Save(writer, builder);
                }
                writer.WriteLine(";\n}");
                return;
            }
            if (Type != null && this is not Constructor) {
                SaveReturnType(writer);
                writer.WriteLine(" __RETURN__ = 0;");
            }
            base.Save(writer, builder);
            if (this is Constructor) {
                writer.WriteLine("return this;");
            } else {
                writer.WriteLine("__DONE__:\n;");
                writer.Write("return");
                if (Type != null) {
                    writer.Write(" __RETURN__");
                }
                writer.WriteLine(";");
            }
            writer.WriteLine("}\n");
        }

        public override string ToString() {
            return Token.Value + (Parameters != null && Parameters.Children.Count > 0 ? "(" + string.Join(", ", Parameters.Children.Select(c => c.Token.Value)) + ")" : "");
        }
    }
}
