using System.IO;

namespace Run {
    public class Return : ContentExpression {

        public override void Parse() {
            if (Scanner.IsEOL()) return;
            Content = ExpressionHelper.Parse(this);
        }

        public override void Print() {
            base.Print();
            Content?.Print();
        }
    }
}