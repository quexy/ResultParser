using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ResultParser
{
    public interface IFileProvider
    {
        TSqlFragment ParseProcedure(string name);
    }

    public class FileProvider : IFileProvider
    {
        private readonly string[] searchPaths;
        private readonly TSqlParser parser = TSqlParser.CreateParser(SqlVersion.Sql170, true);
        public FileProvider(string searchRoot)
        {
            searchPaths = new[] { "", "Mobility", "Web" }
                .Select(d => Path.Combine(searchRoot, d))
                .ToArray();
        }

        public TSqlFragment ParseProcedure(string name)
        {
            using (var stream = File.OpenRead(FindFile(name)))
            using (var reader = new StreamReader(stream))
            {
                var fragment = parser.Parse(reader, out var errors);
                if (errors.Count == 0) return fragment;

                throw new AggregateException($"Failed to parse '{name}'",
                    errors.Select(e => new InvalidOperationException(
                        $"{e.Message} at line: {e.Line}, col: {e.Column}"))
                );
            }
        }

        private static readonly Regex spNameRx = new Regex(
            @"^((\[?[a-z0-9_]+\]?\.)?(\[?[a-z0-9_]+\]?)?\.)?\[?(?<n>[a-z0-9_]+)\]?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private string FindFile(string name)
        {
            var match = spNameRx.Match(name);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"Could not determine procedure name from '{name}'");
            }

            var spName = match.Groups["n"].Value + ".sql";
            var paths = searchPaths
                .Select(p => Path.Combine(p, spName))
                .Where(File.Exists).ToArray();
            if (paths.Length != 0)
            {
                throw new InvalidOperationException(
                    $"No or ambiguous file for '{spName}' (from '{name}')");
            }

            return paths[0];
        }
    }
}
