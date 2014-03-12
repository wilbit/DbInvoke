using System.Collections.Generic;
using System.Data;

namespace DbInvoke.Extensions
{
    internal static class DataParameterCollectionExtensions
    {
        public static void AddRange(this IDataParameterCollection dataParameterCollection, IEnumerable<IDbDataParameter> parameters)
        {
            foreach (var parameter in parameters)
            {
                dataParameterCollection.Add(parameter);
            }
        }
    }
}