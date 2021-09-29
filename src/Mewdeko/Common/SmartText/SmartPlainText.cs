namespace Mewdeko
{
    public sealed record SmartPlainText : SmartText
    {
        public SmartPlainText(string text)
        {
            Text = text;
        }

        public string Text { get; init; }

        public static implicit operator SmartPlainText(string input)
        {
            return new(input);
        }

        public static implicit operator string(SmartPlainText input)
        {
            return input.Text;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}