namespace Run {
    public class Token {
        public static readonly Token Empty = new Token() { Value = string.Empty };
        public string Value { get; set; }
        public int Position;
        internal int Column;
        public Scanner Scanner;
        internal int Line;
        internal TokenType Family;
        internal TokenType Type;

        public int Length => Value?.Length ?? 0;

        internal Token() { }

        internal Token(string value) {
            Value = value;
        }
        internal Token(TokenType type) {
            Type = type;
        }

        public override string ToString() {
            return Value ?? Type.ToString();
        }
    }
}
