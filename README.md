# CacheLily Usage and Examples

CacheLily is a powerful caching and prediction library designed to optimize your code by storing results, predicting outputs, and improving performance for both simple and complex operations. This guide provides an overview of how to use CacheLily effectively, along with practical examples from basic calculations to more complex scenarios.

## Getting Started with CacheLily

Below is an example

```csharp
using CacheLily.Attributes;
using System.Diagnostics;

namespace CacheLily.Test
{
    public class Test
    {
        public static double Add(double sec0, double sec1)
        {
            Thread.Sleep(100); // Simulating a delay to show caching effectiveness
            return sec0 + sec1;
        }

        [NoPredicting]
        public static dynamic Something(object[] args)
        {
            // No prediction or caching
            return 1;
        }

        [NoCaching] // Won't cache or predict
        public static dynamic Something1()
        {
            return 1L;
        }

        public class Object2 : ICacheable
        {
            public int x;
            public int y;

            public int CacheCode { get; set; }
        }

        public static Object2 Object(int x, int y) => new() { x = x, y = y };

        public static void Main()
        {
            // Simple Example with Add method
            Cache cache = new(10);
            for (int i = 0; i < 30000; i++)
            {
                double result = cache.Invoke<double>(Add, i, i); // Cached and possibly predicted output
                Console.WriteLine(result);
            }

            // Using cache with complex object
            Cache<Object2> cache1 = new(capacity: 10, expireAfterCalls: 30, predictiveMode: true);
            Console.WriteLine(cache1.Invoke(Object, 1, 2).x);

            var ob = cache1.New(new Object2() { x = 1, y = 2 }); // Cached copy
            var ob1 = cache1.NewRef(new Object2() { x = 1, y = 2 }); // Cached reference
        }
    }
}
```

## Basic Examples

### 1. Simple Addition with Prediction
The following code uses CacheLily to cache and predict the result of an addition operation. This is particularly useful when you need to repeat similar calculations multiple times.

```csharp
public static double Add(double a, double b)
{
    return a + b;
}

Cache cache = new(10);
for (int i = 0; i < 1000; i++)
{
    double result = cache.Invoke<double>(Add, i, i);
    Console.WriteLine(result); // Output will be cached and possibly predicted for similar inputs
}
```

### 2. Caching Objects
CacheLily also supports caching complex objects. In the example below, we use an `Object2` class that holds two integer properties.

```csharp
public class Object2 : ICacheable
{
    public int x;
    public int y;
    public int CacheCode { get; set; }
}

Cache<Object2> cache = new(10, expireAfterCalls: 5, predictiveMode: true);
var obj = cache.Invoke(Object, 5, 10);
Console.WriteLine(obj.x); // The object with values (5, 10) is cached for subsequent use
```

### 3. Handling Non-Predictable Methods
CacheLily allows you to mark certain methods with attributes like `[NoCaching]` or `[NoPredicting]` to prevent them from being cached or predicted.

```csharp
[NoPredicting]
public static dynamic DoSomething(object[] args)
{
    // This method won't have its output predicted
    return "No prediction here";
}

[NoCaching]
public static dynamic DoAnotherThing()
{
    // This method won't be cached
    return DateTime.Now;
}
```

## Complex Use Cases

### 1. Game Development: Calculating Physics
In a game environment, you often need to calculate complex physics equations, like velocity or collision detection. With CacheLily, you can reduce redundant calculations and predict results based on repeated similar inputs.

```csharp
public static double CalculateVelocity(double mass, double force)
{
    return force / mass;
}

Cache cache = new(20);
for (int i = 0; i < 5000; i++)
{
    double velocity = cache.Invoke<double>(CalculateVelocity, 10.0, i * 2.0);
    Console.WriteLine(velocity); // Predictions can be made for similar mass-force pairs
}
```

### 2. Machine Learning: Repeated Calculations
Suppose you have a machine learning model that needs to calculate the same weight updates during different epochs. CacheLily can help to reduce repeated calculations by storing and predicting results for specific weight and gradient inputs.

```csharp
public static double UpdateWeight(double weight, double gradient, double learningRate)
{
    return weight - learningRate * gradient;
}

Cache cache = new(50, expireAfterCalls: 100, predictiveMode: true);
for (int epoch = 0; epoch < 10000; epoch++)
{
    double updatedWeight = cache.Invoke<double>(UpdateWeight, 0.5, 0.01 * epoch, 0.001);
    Console.WriteLine(updatedWeight); // Cached and predicted results speed up training
}
```


