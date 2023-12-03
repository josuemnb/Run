using System.Collections.Generic;
using System.IO;

namespace Run.V12 {

    public class Block : AST {
        static internal int DeferCounter = 0;
        public List<AST> Children = new(0);
        public List<Defer> Defers = new(0);
        internal bool Validated;
        public T Add<T>() where T : AST, new() => Add(new T());

        public T Add<T>(T item) where T : AST {
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
                    case TokenType.EOL: continue;
                    case TokenType.AT: ParseAnnotation(); continue;
                    case TokenType.COMMENT: Scanner.SkipLine(); continue;
                    case TokenType.NAME: ParseName(token); break;
                    case TokenType.INCREMENT:
                    case TokenType.DECREMENT:
                    case TokenType.OPEN_PARENTESES:
                        Scanner.RollBack();
                        Add<Expression>().Parse();
                        break;
                    default:
                        Program.AddError(token, Error.InvalidExpression);
                        break;
                }
                if (once) return;
            }
        }

        private void ParseAnnotation() {
            var func = FindParent<Function>();
            if (func != null) {
                Program.AddError(Scanner.Current, Error.AnnotationsNotAllowedInsideFunctionScope);
                return;
            }
            Add<Annotation>().Parse();
        }

        public IEnumerable<T> Find<T>() where T : AST {
            for (int i = 0; i < Children.Count; i++) {
                var child = Children[i];
                if (child is T t) yield return t;
                if (child is Block block) {
                    foreach (var r in block.Find<T>()) {
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

        AST FindInFunction(AST a, Function f, string name) {
            if (f.Token.Value == name) return f;
            if (f.Real == name) return f;
            if (a is Module) return null;
            if (f.Parameters != null) {
                foreach (Parameter p in f.Parameters.Children) {
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
            Add<Expression>().Parse();
        }

        public override void Print() {
            base.Print();
            foreach (var child in Children) {
                child.Print();
            }
        }

        public override void Save(TextWriter writer, Builder builder) => SaveBlock(this, writer, builder);

        internal static void SaveBlock(Block block, TextWriter writer, Builder builder) {
            if (block.Defers.Count > 0) {
                writer.Write("int __DEFER_STAGE__");
                writer.Write(block.Defers[0].ID);
                writer.WriteLine(" = 0;");
            }
            for (int i = 0; i < block.Children.Count; i++) {
                var child = block.Children[i];
                if (child is Parameter) continue;
                child.Save(writer, builder);
                writer.Write(child is Expression ? ";\n" : "");
                writer.Write(child is Var ? ";\n" : "");
            }
            if (block.Defers.Count > 0) {
                writer.Write("__DEFER__");
                writer.Write(block.Defers[0].ID);
                writer.WriteLine(":\n;");
                var ID = block.Defers[0].ID;
                for (int i = block.Defers.Count - 1; i >= 0; i--) {
                    var defer = block.Defers[i];
                    writer.Write("if (__DEFER_STAGE__");
                    writer.Write(ID);
                    writer.Write(" >= ");
                    writer.Write(defer.Token.Value);
                    writer.WriteLine(") {");
                    SaveBlock(defer, writer, builder);
                    writer.WriteLine("}");
                }
                var p = block.Parent as Block;
                if (p != null && p is not Class) {
                    if (p.Defers.Count > 0) {
                        writer.Write("goto __DEFER__");
                        writer.Write(p.Defers[0].ID);
                        writer.WriteLine(";");
                    }
                }
            }
        }
    }
}
