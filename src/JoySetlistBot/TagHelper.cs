namespace JoySetlistBot;

public static class TagHelper
{
    internal static string Href(string src, string value)
    {
        return $"<a href=\"{src}\">{value}</a>";
    }
}