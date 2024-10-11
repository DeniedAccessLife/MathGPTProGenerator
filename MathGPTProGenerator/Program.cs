using System;
using System.Threading.Tasks;

namespace MathGPTProGenerator
{
    static class Program
    {
        public static async Task Main()
        {
            Utils.Install();
            Chromium.Initialization();

            try
            {
                Utils.Copyright(Chromium.version);
                await Chromium.Start();
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