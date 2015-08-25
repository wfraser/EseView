using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EseDump
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine("usage: EseDump.exe [/recover] <database path> [<table name>[/<index name>] [...]]");
        }

        static async Task Run(IEnumerable<string> args)
        {
            var vm = new EseView.MainViewModel();
            bool recover = false;

            string dbPath = args.First();
            args = args.Skip(1);

            if (args.First() == "/recover")
            {
                recover = true;
                args = args.Skip(1);
            }

            try
            {
                await vm.OpenDatabaseAsync(dbPath, recover);
            }
            catch (Microsoft.Isam.Esent.Interop.EsentDatabaseDirtyShutdownException)
            {
                Console.WriteLine("The database was not shut down cleanly.");
                Console.WriteLine("Use the /recover flag to enable recovery.");
                Usage();
                Environment.Exit(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading database: " + ex.Message);
                Environment.Exit(-2);
            }

            foreach (string tableName in args)
            {
                vm.DumpTable(tableName, Console.Out);
            }
        }

        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Run(args).Wait();
            }
            else
            {
                Usage();
                Environment.Exit(1);
            }
        }
    }
}
