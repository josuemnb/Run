using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Run {
    public class Generic : AST {
        public Token Constraint;
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.Expect(':')) {
                if (GetName(out Constraint) == false) return;
            }
        }
    }

    public class DeclaredType : ValueType {
        public List<Token> Tokens;
        public AST Caller;

        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        again:
            if (Scanner.Expect('.')) {
                if (GetName(out var name)) {
                    Tokens ??= new(1);
                    Tokens.Add(name);
                    goto again;
                }
            }
            //if (Scanner.Expect('<')) {
            //    Generics = ParseGenerics(this);
            //}
            //Caller = Expression_.ParseParentesesOrBrackets(this, Tokens?.Count > 0 ? Tokens[Tokens.Count - 1] : Token);
        }
    }

    public class ArrayOf {
        public Class Type;
        public Annotation Annotation;
    }

    public class Null : Class {
        public Null() {
            IsNative = true;
            IsPrimitive = true;
            Token = new Token {
                Value = "NULL",
            };
        }
    }
    public class Class : Block {
        public static int CounterID = 1;
        public int ID;
        public int Usage = 1;
        public bool IsTemporary;
        public bool IsEnum;
        public bool HasOperators;
        public List<Interface> Interfaces;

        public bool HasInterfaces => Interfaces != null && Interfaces.Count > 0;

        public bool IsAny;
        public Function Dispose;
        public Function toString;
        public bool IsBased => BaseToken != null;
        public bool IsNumber;
        public ArrayOf ArrayOf;
        public Class Base;
        public AST BaseToken;
        public int BaseCount => Base != null ? Base.BaseCount + 1 : 0;

        public override void Parse() {
            SetAccess();
            GetAnnotations();
            ParseNames();
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
            }
            base.Parse();
        }

        public bool IsCompatible(Class cls) {
            if (cls == null) return false;
            if (cls == this) return true;
            if (cls.Real == Real && Token.Value == cls.Token.Value) return true;
            if (HasInterfaces) {
                for (int i = 0; i < Interfaces.Count; i++) {
                    if (Interfaces[i] == cls) return true;
                }
            }
            return Base?.IsCompatible(cls) ?? false;
        }

        public override void GetAnnotations() {
            base.GetAnnotations();
            for (int i = 0; i < Annotations.Count; i++) {
                switch (Annotations[i].Token.Value) {
                    case "number":
                        IsNumber = true;
                        break;
                    case "array":
                        ArrayOf = new ArrayOf {
                            Annotation = Annotations[i],
                        };
                        break;
                }
            }
        }

        public IEnumerable<T> FindMembers<T>(string name) where T : AST {
            var temp = Token.Value + "_" + name;
            foreach (var v in Children) {
                if (v is T t && (t.Token.Value == name || t.Real == name || t.Real == temp)) yield return t;
            }
            if (Base != null) {
                foreach (var v in Base.FindMembers<T>(name)) {
                    yield return v;
                }
            }
        }

        public T FindMember<T>(string name) where T : AST {
            var temp = Token.Value + "_" + name;
            foreach (var v in Children) {
                if (v is T t && (t.Token.Value == name || t.Real == name || t.Real == temp)) return t;
            }
            return Base?.FindMember<T>(name);
        }

        private void ParseNames() {
            if (GetName(out Token) == false) return;
            //Real = "_" + Token.Value;
            if (Annotations != null) {
                if (Annotations.Find(a => a.IsNative) is Annotation a) {
                    Real = a.Value;
                    return;
                }
            }
            //if (Scanner.Expect('<')) {
            //    Generics = ParseGenerics(this);
            //    Real += "_" + string.Join("_", Generics.Select(g => g.Token.Value));
            //}
            if (Scanner.Expect(':')) {
                ParseBased();
                ParseInterfaces();
            }
        }

        private void ParseInterfaces() {
            while (Scanner.Expect(",")) {
                Interfaces ??= new(0);
                if (GetName(out Token inter) == false) break;
                if (Interfaces.Exists(i => i.Token.Value == inter.Value)) {
                    Program.AddError(inter, Error.NameAlreadyExists);
                } else {
                    Interface @interface = new() { Token = inter, };
                    Interfaces.Add(@interface);
                    //if (Scanner.Expect('<')) {
                    //    @interface.Generics = ParseGenerics(@interface);
                    //}
                }
            }
        }

        private void ParseBased() {
            BaseToken = new AST();
            BaseToken.SetParent(this);
            if (GetName(out BaseToken.Token) == false) return;
            //if (Scanner.Expect('<')) {
            //    Based.Generics = ParseGenerics(Based);
            //}
        }

        public override AST Find(string token) {
            if (base.Find(token) is AST a) return a;
            return Base?.Find(token);
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
            AST.Print(Base);
            foreach (var child in Children) {
                child.Print();
            }
        }
        //public Class Clone() {
        //    var clone = (Class)MemberwiseClone();
        //    clone.Token = Token.Clone();
        //    clone.Based = new AST {
        //        Token = Token,
        //    };
        //    clone.Base = this;
        //    if (Generics != null) {
        //        clone.Generics = new List<Generic>();
        //    }
        //    if (clone.Children != null) {
        //        clone.Children = new List<AST>(Children.Count);
        //        foreach (var child in Children) {
        //            var c = Clone(child);
        //            c.Parent = clone;
        //            clone.Add(c);
        //        }
        //    }
        //    return clone;
        //}

        //T Clone<T>(T ast) where T : AST {
        //    var clone = ast.Clone();
        //    if (ast is Block block) {
        //        var b = clone as Block;
        //        b.Children = new List<AST>(block.Children.Count);
        //        for (int i = 0; i < block.Children.Count; i++) {
        //            var c = Clone(block.Children[i]);
        //            c.Parent = clone;
        //            b.Add(c);
        //        }
        //    }
        //    return clone;
        //}

        public override string ToString() {
            return Token.Value + (IsBased ? ": " + BaseToken.Token.Value : "");
        }
    }
}
