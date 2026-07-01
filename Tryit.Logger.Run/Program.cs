namespace Tryit.Logger.Run;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var logger = typeof(int).GetLogger("124");

        logger.Error("An error occurred.");

        await Task.Delay(1000);

        Console.ReadKey();
    }
}
