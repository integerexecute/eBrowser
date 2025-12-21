using System.Collections.Generic;

namespace eBrowser
{
    public class Query
    {
        public List<QueryWord> queries { get; set; } = [];
        public Query(string query)
        {
            var split = query.Split(' ');
            for (int i = 0; i < split.Length; i++)
            {
                if (split[i].StartsWith('-'))
                {
                    queries.Add(new QueryWord() { type = QueryType.Remove, word = split[i][1..] });
                    continue;
                }
                else if (split[i].Contains(':'))
                {
                    queries.Add(new QueryWord() { type = QueryType.Unknown, word = split[i][1..] });
                    continue;
                }
                else
                    queries.Add(new QueryWord() { type = QueryType.Add, word = split[i] });
            }
        }

        public class QueryWord
        {
            public QueryType type { get; set; }
            public string word { get; set; } = string.Empty;
        }

        public enum QueryType
        {
            Add,
            Remove,
            Unknown
        }
    }
}
