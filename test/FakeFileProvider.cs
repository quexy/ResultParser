using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ResultParser.Test
{
    public class FakeFileProvider : IFileProvider, IEnumerable<KeyValuePair<string, string>>
    {
        private readonly Dictionary<string, string> scripts = new(StringComparer.OrdinalIgnoreCase);

        #region IEnumerable<KeyValuePair<string, string>> implementation
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => scripts.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => scripts.GetEnumerator();
        public string this[string name] { set => scripts.Add(name, value); }
        #endregion

        public void Setup(string name, string content)
            => scripts.Add(name, content);

        public void Clear() => scripts.Clear();

        public TSqlFragment ParseProcedure(string name)
        {
            var parser = TSqlParser.CreateParser(SqlVersion.Sql170, true);
            using var reader = new StringReader(scripts[name]);
            var fragment = parser.Parse(reader, out var errors);
            if (errors.Count == 0) return fragment;

            throw new AggregateException($"Failed to parse '{name}'",
                errors.Select(e => new InvalidOperationException(
                    $"{e.Message} at line: {e.Line}, col: {e.Column}"))
            );
        }
    }
}
