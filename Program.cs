using System;
using System.Net.Http;
using System.Threading;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace IndependentWork13
{
    class Program
    {
        private static int _apiCallAttempts = 0;
        private static int _dbAttempts = 0;

        public static string CallExternalApi(string url)
        {
            _apiCallAttempts++;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Attempt {_apiCallAttempts}: Calling API...");

            if (_apiCallAttempts <= 2)
                throw new HttpRequestException("Temporary API failure");

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] API call successful!");
            return "API RESULT";
        }

        public static string ConnectToDatabase()
        {
            _dbAttempts++;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DB attempt {_dbAttempts}");

            if (_dbAttempts <= 3)
                throw new Exception("Database connection failed");

            return "DB Data Loaded";
        }

        public static void LongRunningOperation()
        {
            Console.WriteLine("Starting long operation...");
            Thread.Sleep(4000);
            Console.WriteLine("Operation completed!");
        }

        static void Main()
        {
            Console.WriteLine("============== SCENARIO 1: RETRY ==============");

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetry(
                    3,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (ex, delay, attempt, ctx) =>
                    {
                        Console.WriteLine($"Retry {attempt} after {delay.TotalSeconds}s due to: {ex.Message}");
                    });

            try
            {
                var result = retryPolicy.Execute(() => CallExternalApi("https://api.example.com"));
                Console.WriteLine("Final API result: " + result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("API failed after all retries: " + ex.Message);
            }

            Console.WriteLine("\n============== SCENARIO 2: CIRCUIT BREAKER ==============");

            var breakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(
                    2,
                    TimeSpan.FromSeconds(5),
                    (ex, t) => Console.WriteLine($"Circuit opened for {t.TotalSeconds}s due to: {ex.Message}"),
                    () => Console.WriteLine("Circuit closed"),
                    () => Console.WriteLine("Circuit half-open"));

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(1000);
                    breakerPolicy.Execute(() => Console.WriteLine("DB Result: " + ConnectToDatabase()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB operation stopped: {ex.Message}");
            }

            Console.WriteLine("\n============== SCENARIO 3: TIMEOUT ==============");

            var timeoutPolicy = Policy
                .Timeout(3, TimeoutStrategy.Pessimistic, (ctx, t, task) =>
                {
                    Console.WriteLine($"Timeout after {t.TotalSeconds}s!");
                });

            try
            {
                timeoutPolicy.Execute(() => LongRunningOperation());
            }
            catch (TimeoutRejectedException)
            {
                Console.WriteLine("Operation was cancelled due to timeout.");
            }

            Console.WriteLine("\n============== END OF WORK ==============");
        }
    }
}
