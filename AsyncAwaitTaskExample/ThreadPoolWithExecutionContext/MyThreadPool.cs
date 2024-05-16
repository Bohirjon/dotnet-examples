using System.Collections.Concurrent;

namespace AsyncAwaitTaskExample.ThreadPoolWithExecutionContext;

public static class MyThreadPool
{
    private static readonly BlockingCollection<(Action action, ExecutionContext executionContext)> WorkItems = new();
    public static void QueueUserWorkItem(Action action) => WorkItems.Add((action, ExecutionContext.Capture()));

    static MyThreadPool()
    {
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    var workItem = WorkItems.Take();
                    ExecutionContext.Run(workItem.executionContext, state => workItem.action.Invoke(), null);
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
        AsyncLocal<int> asyncLocal = new(); // works with execution context
        for (var i = 0; i < 1000; i++)
        {
            // this is setting the value in execution context, so later we can use it in another thread
            asyncLocal.Value = i;
            MyThreadPool.QueueUserWorkItem(() => Console.WriteLine($"Hello {asyncLocal.Value}"));
        }
    }
}