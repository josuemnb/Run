namespace Run {
    public class Token {
        public static readonly Token Empty = new() { Value = string.Empty };

        public static int TokenCount = 0;
        public string Value { get; set; }
        public int ID = TokenCount++;
        public int Position;
        internal int Column;
        public Scanner Scanner;
        internal int Line;
        internal TokenType Family;
        internal TokenType Type;

        public int Length => Value?.Length ?? 0;

        internal Token() {

        }

        internal Token(string value) {
            Value = value;
        }
        internal Token(TokenType type) {
            Type = type;
        }

        public override string ToString() {
            return Value ?? Type.ToString();
        }

        public Token Clone() {
            var clone = MemberwiseClone() as Token;
            clone.ID = TokenCount++;
            return clone;
        }
    }
}
