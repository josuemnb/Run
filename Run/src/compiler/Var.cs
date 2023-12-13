using System.Collections.Generic;
using System.IO;

namespace Run {
    public class Global : Var { }
    public class Field : Var {
    }
    public class Var : ValueType {
        public List<ExpressionV2> Arrays;
        bool Caller = false;
        public ExpressionV2 Initializer;
        public int Usage = 0;

        public override void Parse() {
            SetAccess();
            if (Parent is Class cls && cls.Access == AccessType.STATIC && Access != AccessType.STATIC) {
                Program.AddError(Scanner.Current, Error.IncompatibleAccessClassStatic);
            }
            GetAnnotations();
            if (GetName(out Token) == false) return;
            if (Scanner.Expect(':')) {
                GetReturnType();
            }
            if (Scanner.Expect('=')) {
                ParseInitializer();
                if (Scanner.Current.Type != TokenType.EOL && Scanner.Current.Type != TokenType.SEMICOLON) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                }
            } else if (Scanner.IsEOL() == false && Scanner.Expect(';') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            }
        }

        public void ParseInitializer() {
            Initializer = new();
            Initializer.SetParent(this);
            Initializer.Parse();
        }

        public void GetReturnType() {
            if (GetName(out Token type) == false) return;
            Type = new Class() {
                Token = type,
                IsTemporary = true,
            };
            Type.SetParent(this);
            //SetParentGenerics<Class>(type.Value);
            //SetParentGenerics<Function>(type.Value);
            if (Scanner.Expect('[')) {
                ParseParameters();
            } else if (Scanner.Expect('(')) {
                Caller = true;
                ParseParameters();
            }
        }

        //void SetParentGenerics<T>(string name) where T : Block {
        //    if (FindParent<T>() is T parent && parent.HasGenerics) {
        //        if (parent.Generics.Find(g => g.Token.Value == name) is Generic generic) {
        //            Generics ??= new(1);
        //            Generics.Add(generic);
        //        }
        //    }
        //}

        void ParseParameters() {
            Arrays = new(0);
            if (Caller == false && Scanner.Expect(']')) {
                return;
            } else if (Caller && Scanner.Expect(')')) {
                return;
            }
        again:
            var exp = new ExpressionV2();
            exp.SetParent(this);
            exp.Parse();
            Arrays.Add(exp);
            if (Scanner.Expect(',')) {
                goto again;
            }
            if (Caller == false && Scanner.Expect(']') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
            } else if (Caller && Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
            }
        }

        public override void Print() {
            base.Print();
            Initializer?.Print();
        }

        public override void Save(TextWriter writer, Builder builder) {
            bool array = true;
            if (this is Parameter p && p.Constraints != null) {
                if (p.IsVariadic) {
                    writer.Write("int len_");
                    writer.Write(p.Token.Value);
                    writer.Write(", ...");
                    return;
                } else {
                    writer.Write("void* ");
                }
            } else {
                if (Type == null) {
                    if (Initializer != null) {
                        Program.Validator?.Validate(Initializer);
                        Type = Initializer.Type;
                    }
                }
                if (Type == null) {
                    Error.NullType(this);
                    return;
                }
                writer.Write(Type.Real ?? Type.Token.Value);
                writer.Write(' ');
                if (Type.IsPrimitive == false) {
                    writer.Write('*');
                } else if (Initializer != null) {
                    switch (Initializer.Result) {
                        case New: writer.Write("*"); break;
                        case Cast cast when cast.Arrays > 0: writer.Write("*"); break;
                    }
                }
                if (Arrays != null && Caller == false) {
                    array = false;
                    writer.Write('*');
                }
            }
            writer.Write(Real ?? Token.Value);
            if (FindParent<Function>() != null) {
                SaveInitializer(writer, builder, array);
            }
        }

        public void SaveInitializer(TextWriter writer, Builder builder, bool array = true) {
            if (array && Arrays != null && Caller == false) {
                writer.Write('[');
                if (Arrays.Count > 0) {
                    Arrays[0].Save(writer, builder);
                }
                writer.Write(']');
            } else if (Initializer != null && Parent is not Class) {
                writer.Write(" = ");
                Initializer.Save(writer, builder);
            }
        }

        public override string ToString() {
            return Token.Value + (Type != null ? " : " + Type.Token.Value : "");
        }
    }
}
