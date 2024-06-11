using System.IO;

namespace Run {
    public enum PropertyKind {
        None = 0,
        Getter = 1,
        Setter = 2,
        Initializer = 4,
    }


    public class Property : GetterSetter {
        public override void Parse() {
            if (GetName(out Token) == false) return;
            if (Scanner.Expect(':') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingDeclare);
                return;
            }
            if (GetName(out Token type) == false) return;
            Type = new Class {
                Token = type,
                IsTemporary = true,
            };
            switch (Scanner.Test().Value) {
                case "=": Scanner.Scan(); ParseInitializer(); break;
                case "{": Scanner.Scan(); ParseAll(); break;
                default: ParseGetter(); break;
            }
        }

        public override void Print() {
            base.Print();
            Getter?.Print();
            Setter?.Print();
        }
    }
}