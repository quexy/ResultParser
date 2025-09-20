using System.Diagnostics.CodeAnalysis;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ResultParser
{
    /// <summary>
    /// Collects the column names of all result sets of a stored procedure
    /// including possible embedded stored procedure calls recursively.
    /// </summary>
    /// <remarks>Example code:
    /// <code>
    /// var collector = new ResultListCollector(fileProvider);
    /// fileProvider.ParseProcedure(spName).Accept(collector);
    /// // do whatever you want with `collector.ResultSets`
    /// </code>
    /// </remarks>
    public class ResultListCollector : TSqlConcreteFragmentVisitor
    {
        private readonly IFileProvider fileProvider;
        public ResultListCollector(IFileProvider fileProvider)
        {
            this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        }

        public List<List<string>> ResultSets { get; } = [];

        private int selectDepth = 0;
        public override void Visit(QuerySpecification node)
        {
            selectDepth++;
            base.Visit(node);
            selectDepth--;
        }

        public override void Visit(SelectStatement node)
        {
            if (selectDepth > 0) return;
            if (node.Into is not null) return;

            var querySpec = GetQuerySpecification(node.QueryExpression);
            if (querySpec is null) return; // cannot find specs

            var columnSet = CollectColumns(querySpec);
            // don't add column set if it doesn't have any columns
            if (columnSet.Count > 0) ResultSets.Add(columnSet);
        }

        public override void Visit(ExecuteStatement node)
        {
            if (selectDepth > 0) return;

            var executable = node.ExecuteSpecification.ExecutableEntity;
            if (executable is not ExecutableProcedureReference procRef) return;

            var name = procRef.ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
            var collector = new ResultListCollector(fileProvider);
            fileProvider.ParseProcedure(name).Accept(collector);
            ResultSets.AddRange(collector.ResultSets);
        }

        private static QuerySpecification? GetQuerySpecification(QueryExpression expr) => expr switch
        {
            QuerySpecification qs => qs,
            BinaryQueryExpression binary => GetQuerySpecification(binary.FirstQueryExpression),
            _ => null
        };

        private static List<string> CollectColumns(QuerySpecification query)
        {
            var columnSet = new List<string>();
            foreach (var element in query.SelectElements)
            {
                // get column name, return null if safe
                // or fail when 
                var columnName = GetColumnName(element);
                // variable assignments are not displayed
                if (columnName == null) continue;
                // all other column names are reported
                columnSet.Add(columnName);
            }
            return columnSet;
        }

        private static string? GetColumnName(SelectElement element) => element switch
        {
            SelectScalarExpression scalarExpr when scalarExpr.Expression is ColumnReferenceExpression colRef
                => (scalarExpr.ColumnName?.Value ?? colRef.MultiPartIdentifier.Identifiers[^1].Value) ?? UnnamedColumn(element),

            SelectScalarExpression scalarExpr when scalarExpr.Expression is FunctionCall functionCall
                => (scalarExpr.ColumnName?.Value ?? functionCall.FunctionName.Value) ?? UnnamedColumn(element),

            SelectScalarExpression scalarExpr => scalarExpr.ColumnName?.Value ?? UnnamedColumn(element),

            SelectSetVariable _ => null, // omit '@var = t.col'

            SelectStarExpression _ => throw new InvalidOperationException($"Encountered 'SELECT *' {Location(element)}"),

            _ => throw new InvalidOperationException($"Unexpected select element type {Location(element)}.")
        };

        [DoesNotReturn]
        private static string UnnamedColumn(SelectElement element)
            => throw new InvalidOperationException($"No column name for element {Location(element)}");

        private static string Location(TSqlFragment fragment)
            => $"at line: {fragment.StartLine}"
             + $", column: {fragment.StartColumn}";
    }
}
