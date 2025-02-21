using CacheLily.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;


namespace CacheLily.Test
{
    public class Object1 : ICacheable
    {
        public int CacheCode { get; set; }
        public string Example { get; set; }
    }

    public static class CacheExample
    {
        public static void Main()
        {
            // Initialize cache
            var cache = new Cache<Object1>(capacity: 20, expireAfterCalls: 1, predictiveMode: false);

            // Add objects to cache and demonstrate retrieval
            Object1 firstObject = cache.New(new Object1 { Example = "1" });
            Object1 cachedObject = cache.New(new Object1 { Example = "1" }); // Returns cached object

            ref Object1 cachedObjectRef = ref cache.NewRef(new Object1 { Example = "1" }); // Returns cached object by reference
            var example1 = cache.Invoke(typeof(Object1), GetObject1, "ME");
            var example2 = cache.Invoke(typeof(Object1), GetObject1, "ME"); // Cached value

            Console.WriteLine(example1.Example);
            Console.WriteLine(example2.Example);

            // Performance comparison with and without cache
            string[] array = GenerateRandomStrings(1, 200);
            MeasurePerformance(WithCache, array, "With Cache");
            MeasurePerformance(WithoutCache, array, "Without Cache");
        }

        public static Object1 GetObject1(string value) => new Object1 { Example = value };

        private static string[] GenerateRandomStrings(int length, int stringLength)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            return Enumerable.Range(0, length)
                .Select(_ => new string(Enumerable.Repeat(chars, stringLength)
                .Select(s => s[random.Next(s.Length)]).ToArray()))
                .ToArray();
        }

        public static void MeasurePerformance(Action<string[]> action, string[] data, string label)
        {
            var stopwatch = Stopwatch.StartNew();
            action(data);
            stopwatch.Stop();

            Console.Clear();
            Console.WriteLine($"Took {label}: {stopwatch.ElapsedMilliseconds} milliseconds, {stopwatch.Elapsed.TotalSeconds} seconds.");
            Console.ReadLine();
        }

        public static void WithCache(string[] strings)
        {
            var cache = new Cache(100, 10000000, false);
            var random = new Random();

            for (int i = 0; i < 30000; i++)
            {
                string result = cache.Invoke<string>(GetIndex, strings, random.Next(0, strings.Length));
                Console.WriteLine(result);
            }

            Console.WriteLine("Done");
        }
      
        public static void WithoutCache(string[] strings)
        {
            var random = new Random();

            for (int i = 0; i < 30000; i++)
            {

                string result = GetIndex(strings, random.Next(0, 3));
                Console.WriteLine(result);
            }

            Console.WriteLine("Done");
        }
 
        public static string GetIndex(string[] strings, int index)
        {
            Thread.Sleep(100);
            if (index >= 0 && index < strings.Length)
            {
                return strings[index];
            }
            return null;
        }
    }
}
