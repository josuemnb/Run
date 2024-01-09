using System;
using System.Collections.Generic;
using System.IO;

namespace Run {

    public class Default : Block {
        public Class Type;
        public int Index = 0;
        public bool SameType = true;
        public override void Parse() {
            if (Scanner.Expect("=>")) {
                if (Scanner.Test().Value == "return") {
                    Scanner.Scan();
                    Add<Return>().Parse();
                    return;
                }
                Add<Expression>().Parse();
                return;
            }
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            ParseBlock();
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (SameType && Type.IsPrimitive) {
                writer.Write("default:");
            } else {
                writer.Write("else ");
            }
            writer.WriteLine(" {");
            base.Save(writer, builder);
            writer.WriteLine("}");
        }
    }
    public class Case : Default {
        internal List<Expression> Expressions = new List<Expression>(0);
        public int Count = 0;
        public override void Parse() {
            Token = Scanner.Test();
            if (Token.Family == TokenType.LOGICAL) {
                SameType = false;
                if (FindParent<Switch>() is Switch sw) {
                    sw.SameType = false;
                }
            }
        again:
            var exp = new Expression();
            exp.SetParent(this);
            exp.Parse();
            Expressions.Add(exp);
            if (Scanner.Expect(',')) {
                goto again;
            }
            base.Parse();
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (SameType && Type.IsPrimitive) {
                foreach (var exp in Expressions) {
                    writer.Write("case (");
                    exp.Save(writer, builder);
                    writer.Write("):");
                }
            } else {
                var sw = FindParent<Switch>();
                if (Index > 0) writer.Write("else ");
                writer.Write("if(");
                sw.Expression.Save(writer, builder);
                for (int i = 0; i < Expressions.Count; i++) {
                    var exp = Expressions[i];
                    if (i > 0) {
                        writer.Write(" || ");
                    }
                    writer.Write("(");
                    if (exp.Result is Unary) {
                    } else {
                        writer.Write(" == ");
                    }
                    exp.Save(writer, builder);
                    writer.Write(")");
                }
                writer.Write(")");
            }
            writer.WriteLine(" {");
            SaveBlock(this, writer, builder);
            writer.WriteLine("}");
        }
    }

    public class Switch : Block {
        internal Expression Expression;
        public Class Type;
        public bool SameType = true;
        public override void Parse() {
            Expression = new Expression();
            Expression.SetParent(this);
            Expression.Parse();
            if (Expression.Result == null) {
                Program.AddError(Scanner.Current, Error.InvalidExpression);
                return;
            }
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            int index = 0;
            while (true) {
                var token = Scanner.Scan();
                if (token == null) return;
                switch (token.Type) {
                    case TokenType.CLOSE_BLOCK: return;
                    case TokenType.EOL: break;
                    case TokenType.COMMENT: Scanner.SkipLine(); break;
                    case TokenType.NAME:
                        switch (token.Value) {
                            case "case":
                                var c = Add<Case>();
                                c.Index = index++;
                                c.Parse();
                                break;
                            case "default":
                                var d = Add<Default>();
                                d.Index = index++;
                                d.Parse();
                                break;
                            default:
                                Program.AddError(token, Error.ExpectingCase);
                                Scanner.SkipBlock();
                                return;
                        }
                        break;
                }
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            if (Type == null) return;
            if (SameType && Type.IsPrimitive) {
                writer.Write("switch (");
                Expression.Save(writer, builder);
                writer.WriteLine(") {");
                base.Save(writer, builder);
                writer.WriteLine("}");
                return;
            }
            base.Save(writer, builder);
        }
    }
}