using System.IO;

namespace Run {
    public class Indexer : GetterSetter {
        public Parameter Index;

        public override void Parse() {
            if (Parent is not Class cls) {
                Program.AddError(Scanner.Current, Error.InvalidExpression);
                return;
            }

            if (GetIndex() == false) return;
            if (CheckOthersIndexes(cls) == false) return;
            if (GetReturnType() == false) return;
            cls.HasIndexers = true;

            switch (Scanner.Test().Value) {
                case "{": Scanner.Scan(); ParseAll(); break;
                default: ParseGetter(); break;
            }
        }

        private bool CheckOthersIndexes(Class cls) {
            if (cls.HasIndexers == false) return true;
            foreach (var child in cls.Children) {
                if (child is Indexer index) {
                    if (index.Index.Type.Token.Value == Index.Type.Token.Value) {
                        Program.AddError(Scanner.Current, Error.NameAlreadyExists);
                        return false;
                    }
                }
            }
            return true;
        }

        public override void SetGetter(Function func) {
            base.SetGetter(func);
            func.Parameters = func.Add<Block>();
            func.Parameters.Add(Index);
        }

        public override void SetSetter(Function func) {
            func.Parameters = func.Add<Block>();
            func.Parameters.Add(Index);
            func.Parameters.Add(new Parameter {
                Token = new Token {
                    Value = "value",
                },
                Type = Type
            });

        }

        bool GetIndex() {
            if (Scanner.Expect('[') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfArray);
                return false;
            }
            if (GetName(out Token name) == false) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                return false;
            }
            Index = new Parameter() {
                Token = name,
            };
            Index.SetParent(this);
            if (Scanner.Expect(':') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingDeclare);
                return false;
            }
            if (GetName(out Token type) == false) return false;
            Index.Type = new Class() {
                Token = type,
                IsTemporary = true,
            };
            Index.Type.SetParent(Index);
            if (Scanner.Expect(']') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfArray);
                return false;
            }
            return true;
        }

        public override bool GetReturnType() {
            if (Scanner.Expect(':') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingDeclare);
                return false;
            }
            if (GetName(out Token type) == false) return false;
            Type = new Class() {
                Token = type,
                IsTemporary = true,
            };
            Type.SetParent(this);
            return true;
        }
    }
}