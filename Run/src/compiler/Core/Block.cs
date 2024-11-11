using System;
using System.Collections.Generic;
using System.IO;

namespace Run {

    public class Block : AST {
        static internal int DeferCounter = 0;
        public List<AST> Children = new(0);
        public List<Defer> Defers = new(0);
        public T Add<T>() where T : AST, new() => Add(new T());

        public T Add<T>(T item) where T : AST {
            if (item == null) return null;
            item.SetParent(this);
            Children.Add(item);
            return item;
        }

        public Block() { }

        public override void Parse() => ParseBlock();

        public void ParseBlock(bool once = false) {
            while (true) {
                var token = Scanner.Scan();
                if (token == null) return;
                switch (token.Type) {
                    case TokenType.CLOSE_BLOCK: return;
                    case TokenType.EOL:
                        Program.LinesCompiled++;
                        continue;
                    case TokenType.AT: ParseAnnotation(); continue;
                    case TokenType.COMMENT:
                        Scanner.SkipLine();
                        continue;
                    case TokenType.NAME: ParseName(token); break;
                    default:
                        Scanner.RollBack();
                        Add(ExpressionHelper.Parse(this));
                        break;
                }
                if (once) return;
            }
        }

        private void ParseAnnotation() {
            if (FindParent<Function>() != null) {
                Program.AddError(Scanner.Current, Error.AnnotationsNotAllowedInsideFunctionScope);
                return;
            }
            Add<Annotation>().Parse();
        }

        public bool Contains(Func<AST, bool> predicate, bool recursive = true) {
            for (int i = 0; i < Children.Count; i++) {
                var child = Children[i];
                if (predicate(child)) return true;
                if (recursive && child is Block block) {
                    if (block.Contains(predicate, recursive)) return true;
                }
            }
            return false;
        }

        public bool Contains<T>(bool recursive = true) where T : AST {
            for (int i = 0; i < Children.Count; i++) {
                var child = Children[i];
                if (child is T t) return true;
                if (recursive && child is Block block) {
                    if (block.Contains<T>(recursive)) return true;
                }
            }
            return false;
        }

        public IEnumerable<T> FindChildren<T>(bool recursive = true) where T : AST {
            if (Children == null || Children.Count == 0) yield break;
            //if (this is T t1) yield return t1;
            for (int i = 0; i < Children.Count; i++) {
                var child = Children[i];
                if (child is T t) yield return t;
                if (recursive && child is Block block) {
                    foreach (var r in block.FindChildren<T>(recursive)) {
                        yield return r;
                    }
                }
            }
        }

        public override AST FindName(string name) {
            if (this is Function f && FindInFunction(this, f, name) is AST a) {
                return a;
            }
            for (int i = 0; i < Children.Count; i++) {
                switch (Children[i]) {
                    case Function f1:
                        if (FindInFunction(this, f1, name) is AST a1) {
                            return a1;
                        }
                        break;
                    case CallExpression call:
                        if (call.Caller is Var cv && cv.Token.Value == name) return cv;
                        break;
                    case Var v when v.Token.Value == name: return v;
                    case For fo:
                        if (fo.Start is Var va && va.Token.Value == name) return va;
                        break;
                    case Class c:
                        if (c.Token.Value == name) return c;
                        if (this is Module) continue;
                        if (c.FindMember<AST>(name) is AST ast) {
                            return ast;
                        }
                        break;
                    case Module m:
                        if (m.Nick == name || m.Token.Value == name) return m;
                        if (m != this) {
                            foreach (var child in m.Children) {
                                if (child.Token.Value == name || child.Real == name) {
                                    return child;
                                }
                            }
                        }
                        break;
                }
            }
            return base.FindName(name);
        }

        static AST FindInFunction(AST a, Function f, string name) {
            if (f.Token.Value == name) return f;
            if (f.Real == name) return f;
            if (a is Module) return null;
            if (f.Parameters != null) {
                foreach (AST p in f.Parameters.Children) {
                    if (p.Token.Value == name) {
                        return p;
                    }
                }
            }
            return null;
        }

        public virtual AST Find(string token) {
            if (Token != null && Token.Value == token) return this;
            for (int i = 0; i < Children.Count; i++) {
                var child = Children[i];
                if (child is Block block) {
                    if (block.Find(token) is AST ast) return ast;
                } else if (child.Token != null && child.Token.Value == token) return child;
            }
            return null;
        }

        private void ParseName(Token token) {
            if (Keywords.Parse(token, this)) return;
            if (FindParent<Function>() == null && this is not Enum) {
                Program.AddError(token, Error.InvalidExpression);
                return;
            }
            Scanner.RollBack();
            //Add<Expression>().Parse();
            Add(ExpressionHelper.Parse(this));
        }

        public override void Print() {
            base.Print();
            foreach (var child in Children) {
                child.Print();
            }
        }
    }
}
