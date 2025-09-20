namespace ResultParser.Demo
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args?.Length is null or 0)
                args = ["test1.sql"];

            var fileProvider = new FileProvider("D:\\Projects\\Nestlé\\ResultParser\\scripts");
            var collector = new ResultListCollector(fileProvider);
            fileProvider.ParseProcedure(args[0]).Accept(collector);

            int i = 0;
            foreach (var columnSet in collector.ResultSets)
            {
                ++i;
                Console.WriteLine($"Result set #{i}");
                foreach (var name in columnSet)
                {
                    Console.WriteLine($"   * {name}");
                }
                Console.WriteLine();
            }
        }
    }
}
