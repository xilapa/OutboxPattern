using System.Text;

namespace Common.Outbox;

public static class Utils
{
    public static string ConcatGuidsToQueryString(IEnumerable<Guid> guids)
    {
        var stringBuilder = new StringBuilder();
        var guidArray = guids as Guid[] ?? guids.ToArray();
        var maxIndex = guidArray.Length - 1;
        for (var i = 0; i < guidArray.Length; i++)
        {
            stringBuilder.Append('\'');
            stringBuilder.Append(guidArray[i]);
            stringBuilder.Append('\'');
            if (i != maxIndex)
                stringBuilder.Append(',');
        }

        return stringBuilder.ToString();
    }
}