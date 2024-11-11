using ISIExtensions;

namespace Run {
    public class Global : Var { }
    public class Field : Var {
    }

    public class Var : Array {
        public Expression Initializer;
        public bool InitializerHasGeneric;
        public int Usage = 0;
        public bool IsConst;
        public bool NeedRegister = false;

        public override bool HasGenerics => base.HasGenerics || Generic != null || InitializerHasGeneric;

        public void Parse(bool full) {
            if (full) {
                SetAccess();
                if (Parent is Class cls && cls.AccessType == AccessType.STATIC && AccessType != AccessType.STATIC) {
                    Program.AddError(Scanner.Current, Error.IncompatibleAccessClassStatic);
                }
                GetAnnotations();
            }
            if (GetName(out Token) == false) return;
            if (Real.IsEmpty()) Real = "_" + Token.Value;
            if (full) {
                if (Scanner.Expect(':')) {
                    GetReturnType();
                }
                bool eol;
                if (Scanner.Expect('=')) {
                    ParseInitializer();
                    if ((eol = Scanner.IsEOL()) == false && Scanner.Expect(';') == false) {
                        Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                    }
                } else if (IsConst) {
                    Program.AddError(Scanner.Current, Error.ExpectingInitializer);
                    Scanner.SkipLine();
                    eol = true;
                } else if ((eol = Scanner.IsEOL()) == false && Scanner.Expect(';') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
                }
                Program.LinesCompiled += eol ? 1 : 0;
            } else {
                //for the loop for var a..10
                if (Scanner.Expect('=')) {
                    ParseInitializer();
                }
            }
        }

        public override void Parse() => Parse(true);

        public void ParseInitializer() {
            Initializer = ExpressionHelper.Parse(this);
            if (Initializer is NewExpression n) {
                IsScoped = n.IsScoped;
            }
        }

        public virtual bool GetReturnType() {
            if (GetName(out Token type) == false) return false;
            Type = new Class() {
                Token = type,
                IsTemporary = true,
            };
            Type.SetParent(this);
            if (Scanner.Expect('[')) {
                TypeArray = true;
                ParseArguments();
            }
            return true;
        }

        public override void Print() {
            base.Print();
            Initializer?.Print();
        }

        public override string ToString() {
            return Token.Value + (Type != null ? " : " + Type.Token.Value : "");
        }
    }
}
