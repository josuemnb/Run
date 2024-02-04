using System.Diagnostics;
using System.IO;

namespace Run {

    public class Label : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Token.Value);
            writer.WriteLine(':');
        }
    }

    public class Goto : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("goto ");
            writer.Write(Token.Value);
            writer.WriteLine(';');
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

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("else ");
            if (Condition != null) {
                base.Save(writer, builder);
            } else {
                writer.WriteLine('{');
                SaveBlock(writer, builder);
                writer.WriteLine('}');
            }
        }
    }

    public class If : Block {
        public Expression Condition;

        public override void Parse() {
            Token = Scanner.Current;
            Condition = Expression.ParseExpression(this);
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

        protected void SaveBlock(TextWriter writer, Builder builder) => base.Save(writer, builder);

        //protected new void ParseBlock() => base.Parse();

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("if(");
            Condition.Save(writer, builder);
            writer.WriteLine(") {");
            SaveBlock(writer, builder);
            writer.WriteLine("}");
        }

        public override void Print() {
            AST.Print(this);
            Condition?.Print();
            foreach (var child in Children) {
                child.Print();
            }

        }
    }
    public class For : Block {
        public AST Start;
        //public Var Var;
        public Expression Condition;
        public Expression Step;
        int Stage = -1;

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
                        current.Parse();
                    } else {
                        //current = Start = new Expression(this);
                        current = Start = Expression.ParseExpression(this);
                        if (current is Iterator) {
                            goto beginOfBlock;
                        }
                    }
                    break;
                case 1:
                    //current = Condition = new Expression(this);
                    current = Condition = Expression.ParseExpression(this);
                    break;
                case 2:
                    //current = Step = new Expression(this);
                    current = Step = Expression.ParseExpression(this);
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

        public override void Save(TextWriter writer, Builder builder) {
            switch (Stage) {
                case -1:
                    writer.Write("while(1");
                    break;
                case 0 when Start is Expression condition:
                    writer.Write("while(");
                    condition.Save(writer, builder);
                    break;
                case 0 when Start is Var var && Condition == null:
                    writer.Write("for(");
                    var.Save(writer, builder);
                    writer.Write(";;");
                    writer.Write(var.Token.Value);
                    writer.Write("++");
                    break;
                default:
                    writer.Write("for(");
                    Start?.Save(writer, builder);
                    writer.Write(';');
                    Condition?.Save(writer, builder);
                    writer.Write(';');
                    Step?.Save(writer, builder);
                    break;
            }
            if (Children.Count > 0) {
                writer.WriteLine(") {");
                base.Save(writer, builder);
                writer.WriteLine("}");
            } else {
                writer.WriteLine(");");
            }
        }

    }
    public class Break : AST {
        public override void Save(TextWriter writer, Builder builder) {
            writer.WriteLine("break;");
        }
    }
    public class Continue : AST {
        public override void Save(TextWriter writer, Builder builder) {
            writer.WriteLine("continue;");
        }
    }

    public class Delete : ValueType {
        public Block Block;
        public override void Parse() {
            Block = new Block();
            Block.SetParent(this);
            bool parenteses = Scanner.Expect('(');
        again:
            if (GetName(out Token name) == false) {
                Scanner.SkipLine();
                return;
            }
            Block.Add<IdentifierExpression>().Token = name;
            if (Scanner.Expect(',')) {
                goto again;
            }
            if (parenteses) {
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
                }
            } else if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            for (int i = 0; i < Block.Children.Count; i++) {
                var child = Block.Children[i];
                if (child is IdentifierExpression id && id.Type is Class cls && cls.Dispose != null) {
                    writer.Write(cls.Dispose.Real);
                    writer.Write("(");
                    writer.Write(id.Token.Value);
                    writer.WriteLine(");");
                }
                writer.Write("DELETE(");
                child.Save(writer, builder);
                writer.Write(")");
                if (i < Block.Children.Count - 1) {
                }
                writer.WriteLine(';');
            }
        }
    }
}
