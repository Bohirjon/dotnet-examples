using System.Collections.Concurrent;

namespace AsyncAwaitTaskExample;

public static class MyThreadPoolExContext
{
    private static readonly BlockingCollection<(Action action, ExecutionContext executionContext)> WorkItems = new();
    public static void QueueUserWorkItem(Action action) => WorkItems.Add((action, ExecutionContext.Capture()));

    static MyThreadPoolExContext()
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