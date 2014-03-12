using System.Linq;

namespace DbInvoke.Helpers
{
    internal static class StringHelper
    {
        public static string JoinNotEmptyStrings(string separator, params string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            var filteredValues = values
                .Where(x => !string.IsNullOrEmpty(x));
            var result = string.Join(".", filteredValues);
            return result;
        }
    }
}