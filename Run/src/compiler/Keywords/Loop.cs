using System.IO;

namespace Run {
    public class For : Block {
        internal AST Start;
        internal Expression Condition;
        internal Expression Step;
        internal int Stage = -1;
        internal bool HasRange;

        public override void Parse() {
        again:
            if (Scanner.Expect("=>")) {
                goto once;
            }
            if (Scanner.Expect('{')) {
                goto end;
            }
            if (Stage == -1) Stage = 0;
            if (Scanner.Expect(';')) {
                Stage++;
                goto again;
            }
            AST current = null;
            switch (Stage) {
                case 0:
                    if (Scanner.Expect("var")) {
                        current = Start = new Var();
                        current.SetParent(this);
                        (current as Var).Parse(false);
                        if (Scanner.Peek() != ';') {
                            Stage++;
                            goto again;
                        }
                    } else {
                        current = Start = ExpressionHelper.Expression(this);
                        if (current is Iterator || current is RangeExpression) {
                            HasRange = current is RangeExpression;
                            goto beginOfBlock;
                        }
                    }
                    break;
                case 1:
                    if (Scanner.Expect("..") && Start is Var) {
                        HasRange = true;
                    }
                    current = Condition = ExpressionHelper.Expression(this);
                    break;
                case 2:
                    //current = Step = new Expression(this);
                    current = Step = ExpressionHelper.Expression(this);
                    break;
            }
            //current?.Parse();
            //if (Stage >= 2) {

            //    Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
            //    return;
            //}
            if (Stage < 2 && (Scanner.Expect(';') || Scanner.Current.Type == TokenType.SEMICOLON)) {
                Stage++;
                //Scanner.Scan();
                goto again;
            }
            //Scanner.RollBack();
            if (current is Expression exp && exp.HasError) {
                return;
            }
        beginOfBlock:
            if (Scanner.Expect("=>")) {
                goto once;
            }
            if (Scanner.Expect("{") == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
        end:
            base.Parse();
            return;
        once:
            ParseBlock(true);
        }

    }
    public class Break : AST {
    }
    public class Continue : AST {
    }
}
