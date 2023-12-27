using System;
using System.Threading.Tasks;

namespace MathGPTProGenerator
{
    static class Program
    {
        public static async Task Main()
        {
            Utils.Install();
            await Chrome.Initialization();

            try
            {
                Utils.Copyright(Chrome.version);
                await Chrome.Start();
            }
            catch (Exception ex)
            {
                Utils.Exception(ex.Message);
            }

            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}