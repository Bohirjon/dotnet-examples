using System.Runtime.CompilerServices;
using AsyncAwaitTaskExample;

// #region ThreadPool
//
// // has issue
// for (var i = 0; i < 1000; i++)
// {
//     MyThreadPool.QueueUserWorkItem(() => { Console.WriteLine(i); });
// }
//
// // no issue
// for (var i = 0; i < 1000; i++)
// {
//     var captured = i;
//     MyThreadPool.QueueUserWorkItem(() => Console.WriteLine(captured));
// }
//
// #endregion
//
// #region ThreadPoolExecutionContext
//
// AsyncLocal<int> asyncLocal = new(); // works with execution context
// for (var i = 0; i < 1000; i++)
// {
//     // this is setting the value in execution context, so later we can use it in another thread
//     asyncLocal.Value = i;
//     MyThreadPoolExContext.QueueUserWorkItem(() => Console.WriteLine($"Hello {asyncLocal.Value}"));
// }
//
// #endregion
//

#region Iterate

// this what async await generates
MyTask.Iterate(DoAsync()).Wait();

static IEnumerable<MyTask> DoAsync()
{
    for (var i = 0; i < 1000; i++)
    {
        yield return MyTask.Delay(1000);
        Console.WriteLine(i);
    }
}

#endregion

// #region WithAwaitKeyword
//
// foreach (var value in Enumerable.Range(0, 1000))
// {
//     await MyTask.Delay(2000);
//     Console.WriteLine(value);
// }
//
// #endregion