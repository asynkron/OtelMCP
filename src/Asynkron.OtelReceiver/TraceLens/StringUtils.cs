using System.Text;
using System.Text.RegularExpressions;

namespace TraceLens;

public static class StringUtils
{
    // Precompiled regex to parse hours (h), minutes (m), and seconds (s) from input strings
    private static readonly Regex TimeSpanRegex = new(@"(\d+)\s*([hms])", RegexOptions.Compiled);
    /// <summary>
    ///     Word wraps the given text to fit within the specified width.
    /// </summary>
    /// <param name="text">Text to be word wrapped</param>
    /// <param name="width">
    ///     Width, in characters, to which the text
    ///     should be word wrapped
    /// </param>
    /// <returns>The modified text</returns>
    public static string WordWrap(string text, int width)
    {
        int pos, next;
        var sb = new StringBuilder();

        // Lucidity check
        if (width < 1)
            return text;

        // Parse each line of text
        for (pos = 0; pos < text.Length; pos = next)
        {
            // Find end of line
            var eol = text.IndexOf(Environment.NewLine, pos);
            if (eol == -1)
                next = eol = text.Length;
            else
                next = eol + Environment.NewLine.Length;

            // Copy this line of text, breaking into smaller lines as needed
            if (eol > pos)
                do
                {
                    var len = eol - pos;
                    if (len > width)
                        len = BreakLine(text, pos, width);
                    sb.Append(text, pos, len);
                    sb.Append(Environment.NewLine);

                    // Trim whitespace following break
                    pos += len;
                    while (pos < eol && char.IsWhiteSpace(text[pos]))
                        pos++;
                } while (eol > pos);
            else sb.Append(Environment.NewLine); // Empty line
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Locates position to break the given line so as to avoid
    ///     breaking words.
    /// </summary>
    /// <param name="text">String that contains line of text</param>
    /// <param name="pos">Index where line of text starts</param>
    /// <param name="max">Maximum line length</param>
    /// <returns>The modified line length</returns>
    private static int BreakLine(string text, int pos, int max)
    {
        // Find last whitespace in line
        var i = max;
        while (i >= 0 && !char.IsWhiteSpace(text[pos + i]))
            i--;

        // If no whitespace found, break at maximum length
        if (i < 0)
            return max;

        // Find start of whitespace
        while (i >= 0 && char.IsWhiteSpace(text[pos + i]))
            i--;

        // Return length of text before whitespace
        return i + 1;
    }
    
    public static TimeSpan CreateTimeSpanFromInput(string input)
    {
        try
        {
            // Variables to store the total hours, minutes, and seconds
            int hours = 0, minutes = 0, seconds = 0;

            // Find matches for hours, minutes, and seconds in the input
            MatchCollection matches = TimeSpanRegex.Matches(input);

            // Process each match to assign hours, minutes, or seconds
            foreach (Match match in matches)
            {
                // Convert the captured number to an integer
                int value = int.Parse(match.Groups[1].Value);

                // Determine whether it's hours, minutes, or seconds
                switch (match.Groups[2].Value)
                {
                    case "h":
                        hours += value;
                        break;
                    case "m":
                        minutes += value;
                        break;
                    case "s":
                        seconds += value;
                        break;
                }
            }

            // Create a TimeSpan from the total hours, minutes, and seconds
            return new TimeSpan(hours, minutes, seconds);
        }
        catch (FormatException ex)
        {
            // Log malformed numeric values
            Console.Error.WriteLine($"Invalid time span format '{input}': {ex.Message}");
            return TimeSpan.Zero;
        }
        catch (ArgumentException ex)
        {
            // Log issues such as null input or invalid TimeSpan arguments
            Console.Error.WriteLine($"Invalid time span value '{input}': {ex.Message}");
            return TimeSpan.Zero;
        }
    }


}