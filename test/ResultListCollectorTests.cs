namespace ResultParser.Test
{
    [TestClass]
    public class ResultListCollectorTests
    {
        [TestMethod]
        public void Collects_ResultSets_From_Main_And_Child_Procedures()
        {
            var fileProvider = new FakeFileProvider
            {
                ["main"] = """
                    ALTER PROCEDURE main(@arg NVARCHAR(5)) AS
                    BEGIN
                        DECLARE @res NVARCHAR(5);
                        DECLARE @var NUMERIC(10);
                        DECLARE @tab TABLE (x INT, z NVARCHAR(5));

                        SET @var = (
                            SELECT COUNT(*)
                            FROM tbl
                        )

                        INSERT INTO @tab (x, z)
                        EXEC dbo.noproc @var = 24;

                        SELECT *
                        INTO #t3
                        FROM t3
                        INNER JOIN @tab AS t
                            ON t.x = t3.x;

                        SELECT x AS [a], y 'b', 'z' AS c FROM #t3;

                        EXEC sp1
                            @param = @arg,
                            @res = @res OUTPUT;

                        SELECT
                            f(x) AS 'p',
                            g(x) AS q,
                            [z] = (
                                SELECT TOP 1 z
                                FROM tt
                                WHERE tt.x = t2.x
                            )
                        FROM dbo.[t2]
                        WHERE EXISTS (
                            SELECT TOP 1 1
                            FROM s
                            WHERE s.x = t2.x
                        );

                        EXEC [dbo].[sp2];

                        SELECT @res AS id;

                        SELECT COUNT(DISTINCT y) FROM t
                    END
                    """,
                ["sp1"] = """
                    ALTER PROCEDURE sp1
                    (
                        @param NVARCHAR(5),
                        @res NVARCHAR(5) OUTPUT
                    )
                    AS
                    BEGIN
                        DECLARE @tab as tab_typ;

                        INSERT INTO @tab(x, z)
                        SELECT a, b FROM t;

                        SELECT TOP 1
                            @res = x
                        FROM @tab
                        ORDER BY z DESC;

                        SELECT
                            y,
                            j.z
                        FROM t4
                        OUTER APPLY (
                            SELECT z
                            FROM tt
                            GROUP BY z
                        ) AS j;
                    END
                    """,
                ["sp2"] = """
                    ALTER PROCEDURE sp2
                    AS
                    BEGIN
                        WITH cte AS (
                            SELECT z
                            FROM tt5
                        )
                        SELECT z v FROM cte
                        WHERE x > 0
                        UNION
                        SELECT z v FROM cte
                        WHERE x = 0;
                    END
                    """
            };

            var collector = new ResultListCollector(fileProvider);
            fileProvider.ParseProcedure("main").Accept(collector);

            var cmp = StringComparer.OrdinalIgnoreCase;
            Assert.AreEqual(6, collector.ResultSets.Count);
            CollectionAssert.AreEqual(new List<string> { "a", "b", "c" }, collector.ResultSets[0], cmp);
            CollectionAssert.AreEqual(new List<string> { "y", "z" }, collector.ResultSets[1], cmp);
            CollectionAssert.AreEqual(new List<string> { "p", "q", "z" }, collector.ResultSets[2], cmp);
            CollectionAssert.AreEqual(new List<string> { "v" }, collector.ResultSets[3], cmp);
            CollectionAssert.AreEqual(new List<string> { "id" }, collector.ResultSets[4], cmp);
            CollectionAssert.AreEqual(new List<string> { "count" }, collector.ResultSets[5], cmp);
        }
    }
}
