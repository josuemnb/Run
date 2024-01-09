namespace Run {
    public class Counter {
        Builder Builder;
        public Counter(Builder builder) {
            Builder = builder;
        }

        public void Count() {
            Count(Builder.Program.Main);
        }

        void Count(AST ast) {
            switch (ast) {
                case Null: break;
                case Module m: Count(m); break;
                case Enum e: Count(e); break;
                case Class c: Count(c); break;
                case Function f: Count(f); break;
                case Var v: Count(v); break;
                case If i: Count(i); break;
                case For w: Count(w); break;
                case Block b: Count(b); break;
                case Caller cl: Count(cl); break;
                case New n: Count(n); break;
                case Return r: Count(r); break;
                case Expression e: Count(e); break;
                //case ExpressionV2 e2: Count(e2); break;
                //case MemberAccess ma: Count(ma); break;
                case Binary b: Count(b); break;
                case Unary u: Count(u); break;
                case Ternary t: Count(t); break;
                case Identifier id: Count(id); break;
                case Parenteses pa: Count(pa); break;
                case TypeOf to: Count(to); break;
                case SizeOf so: Count(so); break;
                case Scope scope: Count(scope); break;
                case Cast cast: Count(cast); break;
            }
        }

        void Count(Module m) {
            if (m == null || m.Usage > 0) return;
            m.Usage++;
            Count(m as Block);
        }

        void Count(Enum e) {
            if (e == null || e.Usage > 0) return;
            e.Usage++;
        }

        void Count(Caller c) {
            Count(c.From);
            Count(c.Function);
            if (c.Parameters != null) {
                for (int i = 0; i < c.Parameters.Count; i++) {
                    Count(c.Parameters[i]);
                }
            }
        }

        void Count(New n) => Count(n.Caller);

        void Count(Return r) => Count(r.Expression);

        void Count(Binary b) {
            Count(b.Left);
            Count(b.Right);
        }

        void Count(Unary u) => Count(u.Right);

        void Count(Ternary t) {
            Count(t.Condition);
            Count(t.IsFalse);
            Count(t.IsTrue);
        }

        void Count(Identifier id) {
            Count(id.From);
            if (id.Type is Class c) {
                Count(c);
            }
        }

        void Count(Parenteses pa) => Count(pa.Expression);

        void Count(TypeOf tp) => Count(tp.Expression);

        void Count(SizeOf so) => Count(so.Expression);

        void Count(Scope scope) {
            Count(scope.Type);
        }

        void Count(Cast cast) {
            Count(cast.Type);
            Count(cast.Expression);
        }

        void Count(MemberAccess ma) {
            Count(ma.Left);
            Count(ma.Right);
        }

        void Count(If i) {
            Count(i.Condition);
            Count(i as Block);
        }

        void Count(For f) {
            Count(f.Start);
            Count(f.Condition);
            Count(f.Step);
            Count(f as Block);
        }

        void Count(Expression e) {
            if (e == null) return;
            Count(e.Result);
        }
        //void Count(ExpressionV2 e) {
        //    if (e == null) return;
        //    Count(e.Result);
        //}

        void Count(Var v) {
            if (v == null || v.Usage > 0) return;
            v.Usage++;
            if (v.Type is Class c) {
                Count(c);
            }
            if (v.Initializer is Expression e) {
                Count(e);
            }
        }

        void Count(Class c) {
            if (c == null || c.Usage > 0) return;
            c.Usage++;
            foreach (var v in c.Children) {
                Count(v as Var);
                //Count(v);
            }
            //Count(c as Block);
            if (c.Base is Class b) {
                Count(b);
            }
        }

        void Count(Function f) {
            if (f == null || f.Usage > 0) return;
            f.Usage++;
            if (f.Parent is Class c) {
                Count(c);
            }
            Count(f.Type);
            Count(f as Block);
        }

        void Count(Block block) {
            for (int i = 0; i < block.Children.Count; i++) {
                Count(block.Children[i]);
            }
        }
    }
}
