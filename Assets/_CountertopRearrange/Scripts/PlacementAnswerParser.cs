using System;
using System.Globalization;

namespace CounterTopRearrange
{
    /// <summary>
    /// Parses a vision-model answer into a normalized placement point.
    /// Expected contract: a JSON object containing "x" and "y" in [0, 1],
    /// origin at the top-left of the image. Surrounding prose or markdown
    /// code fences are tolerated; only the first '{' ... last '}' span is read.
    /// Has no Unity dependencies so the contract stays testable in isolation.
    /// </summary>
    public static class PlacementAnswerParser
    {
        public static bool TryParse(string answer, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            if (string.IsNullOrEmpty(answer))
                return false;

            int start = answer.IndexOf('{');
            int end = answer.LastIndexOf('}');
            if (start < 0 || end <= start)
                return false;

            string obj = answer.Substring(start, end - start + 1);

            float parsedX;
            float parsedY;
            if (!TryReadKey(obj, "x", out parsedX))
                return false;
            if (!TryReadKey(obj, "y", out parsedY))
                return false;
            if (!InUnitRange(parsedX) || !InUnitRange(parsedY))
                return false;

            x = parsedX;
            y = parsedY;
            return true;
        }

        static bool InUnitRange(float value)
        {
            return value >= 0f && value <= 1f;
        }

        static bool TryReadKey(string obj, string key, out float value)
        {
            value = 0f;

            int keyIndex = obj.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIndex < 0)
                keyIndex = obj.IndexOf("'" + key + "'", StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int colon = obj.IndexOf(':', keyIndex);
            if (colon < 0)
                return false;

            int i = colon + 1;
            while (i < obj.Length && char.IsWhiteSpace(obj[i]))
                i++;

            int numberStart = i;
            while (i < obj.Length && IsNumberChar(obj[i]))
                i++;

            if (i == numberStart)
                return false;

            return float.TryParse(
                obj.Substring(numberStart, i - numberStart),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        static bool IsNumberChar(char c)
        {
            return char.IsDigit(c)
                || c == '.'
                || c == '-'
                || c == '+'
                || c == 'e'
                || c == 'E';
        }
    }
}
