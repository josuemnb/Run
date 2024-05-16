namespace Run {
    public class Counter(Builder builder) {
        readonly Builder Builder = builder;

        public void Count() {
            Count(Builder.Program.Main);
        }

        public static void Count(AST ast) {
            switch (ast) {
                case Null: break;
                case Module m: Count(m); break;
                case Enum e: Count(e); break;
                case Class c: Count(c); break;
                case Constructor ctor: Count(ctor); break;
                case Operator o: Count(o); break;
                case Delete del: Count(del); break;
                case Function f: Count(f); break;
                case Var v: Count(v); break;
                case If i: Count(i); break;
                case For w: Count(w); break;
                case Block b: Count(b); break;
                case LiteralExpression lit: Count(lit); break;
                case ContentExpression p: Count(p); break;
                case CallExpression ce: Count(ce); break;
                case AsExpression ae: Count(ae); break;
                case BinaryExpression pb: Count(pb); break;
                case IdentifierExpression ie: Count(ie); break;
                case TernaryExpression te: Count(te); break;
                case Scope scope: Count(scope); break;
            }
        }

        static void Count(Delete del) {
            Count(del.Block);
        }

        static void Count(LiteralExpression lit) {
            Count(lit.Type);
        }

        static void Count(Operator op) {
            Count(op as Function);
        }

        static void Count(Constructor ctor) {
            Count(ctor as Function);
        }

        static void Count(Module m) {
            if (m == null || m.Usage > 0) return;
            m.Usage++;
            Count(m as Block);
        }

        static void Count(Enum e) {
            if (e == null || e.Usage > 0) return;
            e.Usage++;
        }

        static void Count(ContentExpression n) => Count(n.Content);

        static void Count(BinaryExpression b) {
            Count(b.Left);
            Count(b.Right);
        }

        static void Count(TernaryExpression t) {
            Count(t.Condition);
            Count(t.False);
            Count(t.True);
        }

        static void Count(IdentifierExpression id) {
            Count(id.From);
            if (id.Type is Class c) {
                Count(c);
            }
        }

        static void Count(Scope scope) => Count(scope.Type);

        static void Count(If i) {
            Count(i.Condition);
            Count(i as Block);
        }

        static void Count(For f) {
            Count(f.Start);
            Count(f.Condition);
            Count(f.Step);
            Count(f as Block);
        }

        static void Count(CallExpression ce) {
            Count(ce.Caller);
            Count(ce.Function);
            for (int i = 0; i < ce.Arguments.Count; i++) {
                Count(ce.Arguments[i]);
            }
        }

        static void Count(Var v) {
            if (v == null || v.Usage > 0) return;
            v.Usage++;
            if (v.Type is Class c) {
                Count(c);
            }
            if (v.Initializer is Expression e) {
                Count(e);
            }
        }

        static void Count(Class c) {
            if (c == null || c.Usage > 0) return;
            c.Usage++;
            Count(c.Dispose);
            foreach (var v in c.Children) {
                Count(v as Var);
            }
            if (c.Base is Class b) {
                Count(b);
            }
        }

        static void Count(Function f) {
            if (f == null || f.Usage > 0) return;
            f.Usage++;
            if (f.Parent is Class c) {
                Count(c);
            }
            Count(f.Type);
            Count(f as Block);
            if (f.Token.Value == "this") {
                if (f.Parent is Class cls && cls.Base is Class b) {
                    for (int i = 0; i < b.Children.Count; i++) {
                        if (b.Children[i] is Constructor ct && (ct.Parameters?.Children.Count ?? 0) == (f.Parameters?.Children.Count ?? 0)) {
                            if ((ct.Parameters?.Children.Count ?? 0) == 0) {
                                Count(ct);
                                return;
                            }
                            for (int j = 0; j < ct.Parameters.Children.Count; j++) {
                                if (Validator.AreCompatible((ct.Parameters.Children[j] as ValueType).Type, (f.Parameters.Children[j] as ValueType).Type) == false) {
                                    Count(ct);
                                    goto next;
                                }
                            }
                            Count(ct);
                            return;
                        }
                    next:;
                    }
                }
            }
        }

        static void Count(Block block) {
            for (int i = 0; i < block.Children.Count; i++) {
                Count(block.Children[i]);
            }
        }
    }
}
