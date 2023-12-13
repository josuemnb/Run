using System;
using System.IO;

namespace Run {
    public class Scanner : IDisposable {
        internal string Data = null;
        internal int Line = 1;
        internal int Position = -1;
        internal int Column = 0;
        internal string Path;
        private StreamReader Reader;

        public string Address { get; private set; }

        public override string ToString() {
            return Data;
        }

        public void Dispose() {
            Reader?.Close();
            Reader = null;
            Data = null;
        }

        internal Scanner(Stream stream) {
            Reader = new StreamReader(stream);
            //using (var reader = new StreamReader(stream)) {
            //    Data = reader.ReadToEnd();
            //}
        }

        internal Scanner(string path) {
            Path = path;
            Reader = new StreamReader(Path);
            Address = Reader.BaseStream.GetType().GetProperty("Name")?.GetValue(Reader.BaseStream) as string;
        }
        internal virtual Token Scan() {
            if (getToken(out Token t)) {
                if (t.Line == 63) ;
                return t;
            }
            return null;
        }

        internal Token Test() {
            var current = Current;
            if (getToken(out Token t)) {
                RollBack(t);
                Current = current;
                return t;
            }
            return current;
        }

        internal int Index => Position;

        internal void Set(int Position) {
            this.Position = Position;
        }

        public bool Test(TokenType type) {
            var position = Position;
            var current = Current;
            var column = Column;
            var s = Scan();
            Column = column;
            Position = position;
            Current = current;
            return s != null && s.Type == type;
        }

        public bool TestFamily(TokenType type) {
            var position = Position;
            var current = Current;
            var s = Scan();
            Position = position;
            Current = current;
            return s != null && s.Family == type;
        }

        internal void RollBack(Token t) {
            if (t.Value != null) {
                Line = t.Line;
                Position -= t.Value.Length;
                Column -= t.Value.Length;
                Current = null;
            }
        }

        internal void Walk() {
            Position++;
            Column++;
        }

        internal bool SkipLine() {
            bool ret = false;
            while (Position < Data.Length && Data[Position] != '\n') {
                if (Data[Position] == '{') {
                    ret = true;
                }
                Position++;
            }
            Column = 0;
            return ret;
        }

        internal void SkipBlock() {
            int block = SkipLine() ? 1 : 0;
            while (block > 0) {
                if (Data == null || Data.Length == 0 || Position >= Data.Length)
                    return;
                switch (Data[Position]) {
                    case '{':
                        block++;
                        break;
                    case '}':
                        block--;
                        if (block <= 0) {
                            return;
                        }
                        break;
                    case '\n': {
                            Column = 0;
                            if (block <= 0)
                                return;
                        }
                        break;
                }
                Column++;
                Position++;
            }
        }


        bool skip(bool newLines = false) {
            Setup();
            if (Position < 0) {
                Position = 0;
                Column = 0;
            }
            if (Data == null)
                return false;
            if (Data.Length <= Position)
                return false;
            while (Position < Data.Length && (Data[Position] == '\t' || Data[Position] == ' ') || (newLines && (Data[Position] == '\n' || Data[Position] == '\r'))) {
                Position++;
                Column++;
            }
            if (Data.Length <= Position)
                return false;
            return true;
        }

        internal bool Match(string m) {
            if (!skip()) return false;
            if (Position + m.Length >= Data.Length) return false;
            for (int i = 0; i < m.Length; i++) {
                if (Data[Position + i] != m[i]) return false;
            }
            Current = new Token {
                Value = m,
                Column = Column,
                Type = TokenType.NAME,
                Family = TokenType.NAME,
            };
            Column += m.Length;
            Position += m.Length;
            return true;
        }

        public bool HasNext => Position < Data.Length;
        public bool Skip(bool newLines = false) => skip(newLines);
        internal bool Expect(char ch) {
            if (!skip())
                return false;
            if (Position >= Data.Length)
                return false;
            if (Data[Position] != ch) {
                return false;
            }
            Current = new Token() {
                Column = Column,
                Scanner = this,
                Value = ch.ToString(),
                Line = Line,
                Position = Position,
            };
            if (!isValidCharacter(Current)) {
                return false;
            }
            Column += Current.Value.Length;
            return true;
        }

        internal bool Expect(string exp) {
            if (string.IsNullOrEmpty(exp) || !skip())
                return false;
            if (Position + exp.Length >= Data.Length)
                return false;
            for (int i = 0; i < exp.Length; i++) {
                if (Data[Position + i] != exp[i]) {
                    return false;
                }
            }
            Current = new Token() {
                Column = Column,
                Scanner = this,
                Value = exp,
                Line = Line,
                Position = Position,
            };
            Position += exp.Length;
            Column += Current.Value.Length;
            return true;
        }

        public Token Until(string delimeter) {
            Setup();
            var idx = Data.IndexOf(delimeter, Position);
            if (idx < 0) return Token.Empty;
            Token token = new Token { Column = Column, Line = Line, Position = Position, Scanner = this, Value = Data.Substring(Position, idx - Position) };
            Position += token.Value.Length;
            Column += token.Value.Length;
            return token;
        }

        public Token Get(string value) {
            var scan = Scan();
            if (scan == null) {
                return null;
            }
            if (scan.Type != TokenType.NAME || scan.Value != value) {
                RollBack();
                return null;
            }
            return scan;
        }

        public Token GetName(bool rollback) {
            var current = Current;
            var scan = Scan();
            if (scan == null) {
                return null;
            }
            if (scan.Type != TokenType.NAME) {
                if (rollback) {
                    RollBack();
                    Current = current;
                }
                return null;
            }
            return scan;
        }

        public Token GetQuote(bool rollback) {
            var scan = Scan();
            if (scan == null) {
                return null;
            }
            if (scan.Type != TokenType.QUOTE) {
                if (rollback) {
                    RollBack();
                }
                return null;
            }
            return scan;
        }

        internal void RollBack() {
            RollBack(Current);
        }

        internal char Peek() {
            if (!skip()) {
                return char.MaxValue;
            }
            if (!Valid) {
                return char.MaxValue;
            }
            return Data[Position];
        }

        internal bool IsEOL() {
            if (!skip(false))
                return true;
            if (!Valid || Data[Position] == '\n' || Data[Position] == '\r') {
                if (Data[Position] == '\r') {
                    Position++;
                }
                Column = 0;
                return true;
            }
            return false;
        }

        internal void Ignore() {
            getToken(out _);
        }

        public bool Valid => Position < Data.Length;

        public Token Current { get; set; }
        public int StartOfLine { get; private set; }

        void Setup() {
            if (Position == -1 && Reader != null) {
                Position = 0;
                Data = Reader.ReadToEnd();
                Reader.Close();
                Reader = null;
            }
        }

        private bool getToken(out Token tok) {
            Setup();
            bool ok = true;
            Current = tok = new Token() {
                Position = Position,
                Scanner = this,
                Line = Line,
                Column = Column,
            };
            if (Position >= Data.Length) {
                return false;
            }
            if (IsEOL()) {
                Position++;
                Line++;
                tok.Column = Column = 0;
                tok.Type = TokenType.EOL;
                tok.Value = "\n";
                return ok;
            }
            var start = Position;
            if (IsNumber(tok)) {
                goto end;
            } else if (isValidCharacter(tok)) {
                goto end;
            } else if (Valid && char.IsLetter(Data[Position]) || Data[Position] == '_') {
                for (int i = 0; i < 3 && Data[Position] == '_'; i++, Position++) ;
                var p = Position;
                while (Valid && (char.IsLetterOrDigit(Data[Position]) || Data[Position] == '_')) {
                    Position++;
                }
                if (p == Position) {
                    return false;
                }
                if (Valid && Data[Position] == '_') {
                    return false;
                }
                tok.Value = Data.Substring(start, Position - start);
                switch (tok.Value) {
                    case "as":
                        tok.Type = TokenType.AS;
                        tok.Family = TokenType.KEYWORD;
                        break;
                    default:
                        tok.Type = TokenType.NAME;
                        tok.Family = TokenType.NAME;
                        break;
                }
            } else {
                ok = false;
            }
        end:
            Column += tok.Value?.Length ?? 0;
            return ok;
        }

        bool IsNumber(Token tok) {
            if (!Valid) return false;
            if (Data[Position] == '-') {
                if (char.IsDigit(Data[Position + 1])) {
                    Position++;
                    GetNumber(tok, false);
                    return true;
                }
            } else if (char.IsDigit(Data[Position])) {
                GetNumber(tok, true);
                return true;
            }
            return false;
        }

        void GetReal(Token tok) {
            tok.Type = TokenType.REAL;
            while (Valid && char.IsDigit(Data[Position])) {
                Position++;
            }
            if (Valid && (Data[Position] == 'e' || Data[Position] == 'E')) {
                Position++;
                if (Valid && (Data[Position] == '+' || Data[Position] == '-')) {
                    Position++;
                }
                while (Valid && char.IsDigit(Data[Position])) {
                    Position++;
                }
            }
        }

        void GetNumber(Token tok, bool positive) {
            var start = Position;
            tok.Type = TokenType.NUMBER;
            tok.Family = TokenType.LITERAL;
            while (Valid && char.IsDigit(Data[Position])) {
                Position++;
            }
            if (Valid && Data[Position] == '.') {
                Position++;
                GetReal(tok);
            }
            tok.Value = (positive ? "" : "-") + Data.Substring(start, Position - start);
        }

        bool isValidCharacter(Token tok) {
            tok.Type = 0;
            if (!Valid)
                return false;
            var start = Position;
            int len = 0;
            switch (Data[Position]) {
                case '\r':
                case '\n':
                    while (Valid && (Data[Position] == '\r' || Data[Position] == '\n')) {
                        if (Data[Position] == '\n') {
                            Line++;
                        }
                        Position++;
                    }
                    Column = 0;
                    tok.Type = TokenType.EOL;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '#':
                    tok.Type = TokenType.MACRO;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '@':
                    tok.Type = TokenType.AT;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '?':
                    tok.Type = TokenType.TERNARY;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '+':
                    if (Valid && Data[Position + 1] == '+') {
                        Position++;
                        len = 1;
                        tok.Type = TokenType.INCREMENT;
                        tok.Family = TokenType.ARITMETIC;
                    } else if (Valid && Data[Position + 1] == '=') {
                        len = 1;
                        tok.Type = TokenType.PLUS_ASSIGN;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                    } else {
                        tok.Type = TokenType.PLUS;
                        tok.Family = TokenType.ARITMETIC;
                    }
                    tok.Value = Data.Substring(start, (isDouble(Data[Position]) ? 2 : 1) + len);
                    break;
                case '-':
                    if (Valid && Data[Position + 1] == '-') {
                        len = 1;
                        Position++;
                        tok.Type = TokenType.DECREMENT;
                        tok.Family = TokenType.ARITMETIC;
                    } else if (Valid && Data[Position + 1] == '=') {
                        len = 1;
                        tok.Type = TokenType.MINUS_ASSIGN;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                    } else {
                        tok.Type = TokenType.MINUS;
                        tok.Family = TokenType.ARITMETIC;
                    }
                    tok.Value = Data.Substring(start, (isDouble(Data[Position]) ? 2 : 1) + len);
                    break;
                case '/':
                    if (Valid && Data[Position + 1] == '*') {
                        Position++;
                    again:
                        while (Valid && Data[Position] != '*') {
                            Position++;
                        }
                        if (Valid && Data[Position + 1] != '/') {
                            Position++;
                            goto again;
                        }
                        tok.Type = TokenType.COMMENT;
                        tok.Family = TokenType.SYNTAX;
                        len = Position - start;
                    }
                    if (Valid && Data[Position + 1] == '/') {
                        len = 2;
                        Position++;
                        tok.Type = TokenType.COMMENT;
                        tok.Family = TokenType.SYNTAX;
                    } else if (Valid && Data[Position + 1] == '=') {
                        len = 2;
                        tok.Type = TokenType.DIVIDE_ASSIGN;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                    } else {
                        tok.Type = TokenType.DIVIDE;
                        tok.Family = TokenType.ARITMETIC;
                    }
                    tok.Value = Data.Substring(start, (isDouble(Data[Position]) ? 2 : 1) + len);
                    break;
                case '*':
                    if (Valid && Data[Position + 1] == '=') {
                        len = 1;
                        tok.Type = TokenType.MULTIPLY_ASSIGN;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                    } else {
                        tok.Type = TokenType.MULTIPLY;
                        tok.Family = TokenType.ARITMETIC;
                    }
                    tok.Value = Data.Substring(start, (isDouble(Data[Position]) ? 2 : 1) + len);
                    break;
                case ',':
                    tok.Type = TokenType.COMMA;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case ';':
                    tok.Type = TokenType.SEMICOLON;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case ':':
                    tok.Type = TokenType.DECLARE;
                    tok.Family = TokenType.SYNTAX;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '%':
                    tok.Type = TokenType.MOD;
                    tok.Family = TokenType.ARITMETIC;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '{':
                case '}':
                    tok.Family = TokenType.SYNTAX;
                    tok.Type = Data[Position] == '{' ? TokenType.OPEN_BLOCK : TokenType.CLOSE_BLOCK;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '[':
                case ']':
                    tok.Family = TokenType.SYNTAX;
                    tok.Type = Data[Position] == '[' ? TokenType.OPEN_ARRAY : TokenType.CLOSE_ARRAY;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '(':
                case ')':
                    tok.Family = TokenType.SYNTAX;
                    tok.Type = Data[Position] == '(' ? TokenType.OPEN_PARENTESES : TokenType.CLOSE_PARENTESES;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '=':
                    if (Valid && Data[Position + 1] == '=') {
                        Position++;
                        tok.Type = TokenType.EQUAL;
                        tok.Family = TokenType.LOGICAL;
                        tok.Value = Data.Substring(start, 2);
                    } else if (Valid && Data[Position + 1] == '>') {
                        Position++;
                        tok.Type = TokenType.ARROW;
                        tok.Family = TokenType.SYNTAX;
                        tok.Value = Data.Substring(start, 2);
                    } else {
                        tok.Type = TokenType.ASSIGN;
                        tok.Family = TokenType.ARITMETIC;
                        tok.Value = Data.Substring(start, 1);
                    }
                    break;
                case '.':
                    tok.Family = TokenType.SYNTAX;
                    if (Valid && Data[Position + 1] == '.') {
                        Position++;
                        tok.Type = TokenType.RANGE;
                        tok.Value = Data.Substring(start, 2);
                        if (Valid && Data[Position + 1] == '.') {
                            Position++;
                            tok.Type = TokenType.VA_ARGS;
                            tok.Value = Data.Substring(start, 3);
                        }
                    } else {
                        tok.Type = TokenType.DOT;
                        tok.Value = Data.Substring(start, 1);
                    }
                    break;
                case '>':
                    len = 1;
                    tok.Type = TokenType.GREATHER;
                    tok.Family = TokenType.LOGICAL;
                    if (Valid && Data[Position + 1] == '=') {
                        tok.Type = TokenType.GREAT_OR_EQUAL;
                        tok.Family = TokenType.LOGICAL;
                        Position++;
                        len = 2;
                    } else if (Valid && Data[Position + 1] == '>') {
                        tok.Type = TokenType.SHIFT_RIGHT;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                        len = 2;
                    }
                    tok.Value = Data.Substring(start, len);
                    break;
                case '<':
                    len = 1;
                    tok.Type = TokenType.LOWER;
                    tok.Family = TokenType.LOGICAL;
                    if (Valid && Data[Position + 1] == '=') {
                        Position++;
                        if (Valid && Data[Position + 1] == '>') {
                            Position++;
                            tok.Type = TokenType.TREE_WAY;
                            len = 3;
                        } else {
                            tok.Type = TokenType.LOWER_OR_EQUAL;
                            len = 2;
                        }
                    } else if (Valid && Data[Position + 1] == '<') {
                        tok.Type = TokenType.SHIFT_LEFT;
                        tok.Family = TokenType.ARITMETIC;
                        Position++;
                        len = 2;
                    }
                    tok.Value = Data.Substring(start, len);
                    break;
                case '!':
                    len = 1;
                    tok.Type = TokenType.BANG;
                    tok.Family = TokenType.LOGICAL;
                    if (Valid && Data[Position + 1] == '=') {
                        tok.Type = TokenType.DIFFERENT;
                        Position++;
                        len = 2;
                    }
                    tok.Value = Data.Substring(start, len);
                    break;
                case '&':
                    len = 1;
                    tok.Type = TokenType.BITWISE_AND;
                    tok.Family = TokenType.LOGICAL;
                    if (Valid && Data[Position + 1] == '&') {
                        tok.Type = TokenType.AND;
                        Position++;
                        len = 2;
                    } else if (Valid && Data[Position + 1] == '=') {
                        len = 2;
                        tok.Type = TokenType.AND_ASSIGN;
                        Position++;
                    }
                    tok.Value = Data.Substring(start, len);
                    break;
                case '|':
                    len = 1;
                    tok.Type = TokenType.BITWISE_OR;
                    tok.Family = TokenType.LOGICAL;
                    if (Valid && Data[Position + 1] == '|') {
                        tok.Type = TokenType.OR;
                        Position++;
                        len = 2;
                    } else if (Valid && Data[Position + 1] == '=') {
                        len = 2;
                        tok.Type = TokenType.OR_ASSIGN;
                        Position++;
                    }
                    tok.Value = Data.Substring(start, len);
                    break;
                case '^':
                    tok.Type = TokenType.XOR;
                    tok.Family = TokenType.LOGICAL;
                    tok.Value = Data.Substring(start, 1);
                    break;
                case '"':
                    Position++;
                    while (Valid && Data[Position] != '"') {
                        if (Data[Position] == '\\') {
                            switch (Data[Position]) {
                                case '\\':
                                case 't':
                                case 'n':
                                case 'r':
                                case 'b':
                                case '0':
                                case '\'':
                                case '"':
                                    Position++;
                                    break;
                            }
                        }
                        Position++;
                    }
                    tok.Type = TokenType.QUOTE;
                    tok.Family = TokenType.LITERAL;
                    tok.Value = "\"" + Data.Substring(start + 1, Position - start - 1) + "\"";
                    break;
                case '\'':
                    Position++;
                    var temp = Position;
                    bool empty = true;
                    if (Valid && Data[Position] == '\\') {
                        Position++;
                        empty = false;
                        switch (Data[Position]) {
                            case '\\':
                            case 't':
                            case 'n':
                            case 'r':
                            case 'b':
                            case '0':
                            case '\'':
                            case '"':
                                Position++;
                                break;
                            default:
                                return false;
                        }
                    } else if (Valid && Data[Position] != '\'') {
                        Position++;
                        empty = false;
                    }
                    if (Valid && Data[Position] == '\'') {
                        tok.Type = TokenType.CHAR;
                        tok.Family = TokenType.LITERAL;
                        tok.Value = empty ? "0" : ("'" + Data.Substring(temp, Position - temp) + "'");
                    }
                    break;
                default:
                    return false;
            }
            Position++;
            return tok.Type != 0;
        }

        bool isDouble(char ch) {
            if (Valid && (Position + 1) < Data.Length && Data[Position + 1] == ch) {
                Position++;
                return true;
            }
            return false;
        }
    }
}
