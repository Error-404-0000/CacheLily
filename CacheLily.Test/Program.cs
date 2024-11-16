using System.Diagnostics;

namespace CacheLily.Test
{

    public class Test
    {
        public static double Add(double sec0, double sec1)
        {
            Thread.Sleep(100);
            return sec0 + sec1;
        }
        [Obsolete]
        public static void Main()
        {
            Console.ReadLine();
            Cache cache = new(10);
            Random random = new();
            int iterations = 130000;
            int c = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                int newe = random.Next(0, 20);
                double result = cache.Invoke<double>(Add, i, i);
                Console.WriteLine(result);

                if (result != i + i)
                    Console.ReadLine();

            }
            stopwatch.Stop();
            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine($"Cache execution time: {stopwatch.ElapsedMilliseconds} ms");
            Thread.Sleep(3000);
            // Benchmark without Cache
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {

                double result = Add(i, i);
                Console.WriteLine(result);
            }
            Console.Clear();
            stopwatch.Stop();
            Console.WriteLine("==================================================");
            Console.WriteLine($"Direct execution time: {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
