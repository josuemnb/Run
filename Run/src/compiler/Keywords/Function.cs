using System.Collections.Generic;
using System.Linq;

namespace Run {
    public class Parameter : Var {
        public bool IsMember;
        public bool IsVariadic;
        public List<AST> Constraints;
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
        public bool IsDefault;
        public override string ToString() {
            return "Constructor: " + Type;
        }

        public bool BaseCall, ThisCall;
        public List<Expression> ExtraCall;

        public override void Parse() {
            SetAccess();
            if (Scanner.Expect('(') && Scanner.Expect(')') == false) {
                ParseParameters();
            }
            if (Scanner.Expect(':')) {
                switch (Scanner.Test().Value) {
                    case "base": ParseBase(); break;
                    case "this": ParseThis(); break;
                    default: Program.AddError(Scanner.Current, Error.ExpectingBaseOrThis); break;
                }
            }
            GetAnnotations();
            CheckImplicit();
            if (Scanner.Expect("=>")) {
                ParseArrow();
                return;
            }
            ParseBlock();
        }

        private void CheckImplicit() {
            var a = Annotations.Find(a => a.Token.Value == "implicit");
            if (a == null) return;

            if (Parameters.Children.Count != 1) {
                Program.AddError(a.Token, "Implicit Annotation only works 1 parameter size in constructor");
                return;
            }
            var p = Parameters.Children[0] as Parameter;
            if (p.Type.IsNative == false) {
                //Program.AddError(a.Token, "Implicit Annotation only works with native types");
                //return;
            }
            if (p.TypeArray) {
                Program.AddError(a.Token, "Implicit Annotation don't allow array parameter types: " + a.Token.Value);
                return;
            }
            var s = p.Type.Token.Value;
            if (Program.Implicits.TryGetValue(s, out var ast)) {
                Program.AddError(a.Token, "Implicit Annotation already defined for " + s + " in " + ast.Token.Value);
                return;
            }
            Program.Implicits.Add(s, this);
        }

        private void ParseThis() {
            ThisCall = true;
            ParseExtraParameters();
        }

        private void ParseBase() {
            BaseCall = true;
            ParseExtraParameters();
        }

        void ParseExtraParameters() {
            Scanner.Scan();
            ExtraCall = new(0);
            if (Scanner.Expect('(') && Scanner.Expect(')') == false) {
            again:
                ExtraCall.Add(ExpressionHelper.Expression(this));
                if (Scanner.Expect(',')) {
                    goto again;
                }
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                }
            }
        }
    }

    public class Function : Block {
        public Block Parameters;
        public Class Type;
        public Token Pointer;
        public List<string> NativeNames;
        public bool HasDefers;
        public bool IsArrow;
        public bool IsExtension;
        public bool TypeArray;
        public bool HasInterface;
        public bool HasVariadic;
        public int Usage = 0;

        public override void Parse() {
            SetAccess();
            var cls = Parent as Class;
            if (cls != null && cls.Access == AccessType.STATIC && Access != AccessType.STATIC) {
                Program.AddError(Scanner.Current, Error.IncompatibleAccessClassStatic);
            }
            GetAnnotations();
            if (GetName(out Token) == false) return;
            if (Token.Value == "main") {
                Real = Token.Value;
                (Parent as Block).Children.Remove(this);
                (Parent as Block).Add<Main>().Parse();
                return;
            }
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

        internal void ParseArrow() {
            IsArrow = true;
            base.ParseBlock(true);
            if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            } else {
                Program.LinesCompiled++;
            }
        }

        public override AST FindName(string name) {
            if (Parameters != null) {
                foreach (var p in Parameters.Children) {
                    if (p.Token.Value == name) {
                        return p;
                    }
                }
            }
            return base.FindName(name);
        }

        public override AST FindChildren(string name) {
            if (base.FindChildren(name) is AST a) {
                return a;
            }
            if (Parameters != null) {
                foreach (var p in Parameters.Children) {
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

        protected new void ParseBlock(bool once = false) {
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
            }
            base.ParseBlock(once);
        }

        protected void ParseParameters() {
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
                if (Parent is Class == false) {
                    Program.AddError(Scanner.Current, Error.OnlyInClassScope);
                    return;
                }
                param.IsMember = true;
            }
            if (GetName(out param.Token) == false) {
                Scanner.SkipLine();
                return;
            }
            param.Real = "_" + param.Token.Value;
            if (param.IsMember == false) {
                if (Scanner.Expect(':') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingAssign);
                    return;
                }
                param.GetReturnType();
            }
            if (Scanner.Expect(',')) goto again;
            if (Scanner.Expect(')') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingCommaOrCloseParenteses);
                return;
            }
        }

        public override void Print() {
            Print(this);
            if (Annotations != null)
                foreach (var annotation in Annotations)
                    annotation.Print();
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

        public override string ToString() {
            return Token.Value + (Parameters != null && Parameters.Children.Count > 0 ? "(" + string.Join(", ", Parameters.Children.Select(c => c.Token.Value)) + ")" : "");
        }
    }
}
