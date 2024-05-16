using System.Collections.Concurrent;

namespace AsyncAwaitTaskExample;

public static class MyThreadPool
{
    private static readonly BlockingCollection<Action> WorkItems = new();
    public static void QueueUserWorkItem(Action action) => WorkItems.Add(action);

    static MyThreadPool()
    {
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    var action = WorkItems.Take();
                    action();
                }
            })
            {
                IsBackground =
                    true // if true when main thread exits processors does not wait the background threads to be exited 
            }.Start();
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        // has issue
        for (var i = 0; i < 1000; i++)
        {
            MyThreadPool.QueueUserWorkItem(() => { Console.WriteLine(i); });
        }

        // no issue
        for (var i = 0; i < 1000; i++)
        {
            var captured = i;
            MyThreadPool.QueueUserWorkItem(() => Console.WriteLine(captured));
        }
    }
}