using System;
using System.Collections.Generic;
using System.IO;

namespace Run {

    public class Default : Block {
        public Class Type;
        public int Index = 0;
        public bool SameType = true;
        public override void Parse() {
            if (Scanner.Current.Type == TokenType.ARROW) {
                if (Scanner.Test().Value == "return") {
                    Scanner.Scan();
                    Add<Return>().Parse();
                    return;
                }
                Add(ExpressionHelper.Parse(this));
                return;
            }
            if (Scanner.Current.Type != TokenType.OPEN_BLOCK) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            ParseBlock();
        }
    }
    public class Case : Default {
        internal List<Expression> Expressions = new(0);
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
            Expressions.Add(ExpressionHelper.Parse(this));
            Scanner.Scan();
            if (Scanner.Current.Type == TokenType.COMMA) {
                goto again;
            }
            base.Parse();
        }
    }

    public class Switch : Block {
        internal Expression Expression;
        public Class Type;
        public bool SameType = true;
        public override void Parse() {
            Expression = ExpressionHelper.Parse(this);
            if (Expression == null) {
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
                                Scanner.Scan();
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
    }
}