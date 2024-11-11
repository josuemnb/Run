using System.IO;

namespace Run {

    public class Label : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }
    }

    public class Goto : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }
    }
    public class Else : If {
        public override void Parse() {
            if (Scanner.Expect('{')) {
                ParseBlock();
                return;
            }
            if (Scanner.Expect("if")) {
                base.Parse();
            }
        }
    }

    public class If : Block {
        public Expression Condition;

        public override void Parse() {
            Token = Scanner.Current;
            Condition = ExpressionHelper.Parse(this);
            if (Scanner.Expect("=>")) {
                ParseBlock(true);
                return;
            }
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            base.Parse();
            if (Scanner.Expect("else")) {
                (Parent as Block).Add<Else>().Parse();
            }
        }

        public override void Print() {
            Print(this);
            Condition?.Print();
            foreach (var child in Children) {
                child.Print();
            }

        }
    }

}
