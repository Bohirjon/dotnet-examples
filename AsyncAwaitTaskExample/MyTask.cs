using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncAwaitTaskExample;

public class MyTask
{
    private bool _isCompleted;
    private Exception _exception;
    private ExecutionContext _executionContext;
    private Action _continuation;

    public bool IsCompleted => _isCompleted;
    public void SetResult() => Complete(null);
    public void SetException(Exception exception) => Complete(exception);

    private void Complete(Exception exception)
    {
        _isCompleted = true;
        _exception = exception;

        if (_isCompleted)
            throw new Exception("Task is completed");

        if (_continuation is not null)
        {
            MyThreadPoolExContext.QueueUserWorkItem(() =>
            {
                if (_executionContext is not null)
                {
                    ExecutionContext.Run(_executionContext, state => _continuation.Invoke(), null);
                }
                else
                {
                    _continuation.Invoke();
                }
            });
        }
    }

    public void ContinueWith(Action action)
    {
        if (_isCompleted)
        {
            MyThreadPoolExContext.QueueUserWorkItem(action);
        }
        else
        {
            _executionContext = ExecutionContext.Capture();
            _continuation = action;
        }
    }

    public void Wait()
    {
        ManualResetEventSlim manualResetEventSlim = null;

        if (!IsCompleted)
        {
            manualResetEventSlim = new ManualResetEventSlim();
            ContinueWith(manualResetEventSlim.Set);
        }

        manualResetEventSlim?.Wait();

        if (_exception is not null)
            ExceptionDispatchInfo.Throw(_exception);
    }

    public static MyTask Delay(TimeSpan duration)
    {
        var task = new MyTask();
        _ = new Timer(_ => task.SetResult()).Change(duration, TimeSpan.FromMilliseconds(-1));
        return task;
    }

    public static MyTask Iterate(IEnumerable<MyTask> tasks)
    {
        var task = new MyTask();

        var enumerator = tasks.GetEnumerator();

        void MoveNext()
        {
            try
            {
                if (enumerator.MoveNext())
                {
                    enumerator.Current?.ContinueWith(MoveNext);
                    return;
                }
            }
            catch (Exception e)
            {
                task.SetException(e);
                return;
            }

            task.SetResult();
        }

        MoveNext();

        return task;
    }

    public static MyTask Run(Action action)
    {
        var task = new MyTask();
        MyThreadPoolExContext.QueueUserWorkItem(() =>
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                task.SetException(exception);
                return;
            }

            task.SetResult();
        });
        return task;
    }

    public MyTask WhenAll(IList<MyTask> tasks)
    {
        var whenAllTask = new MyTask();

        if (!tasks.Any())
        {
            whenAllTask.SetResult();
        }
        else
        {
            var remainingTasks = tasks.Count;
            var continuation = () =>
            {
                // thread save decrement
                if (Interlocked.Decrement(ref remainingTasks) == 0)
                    whenAllTask.SetResult();
            };
            foreach (var task in tasks)
            {
                task.ContinueWith(continuation);
            }
        }

        return whenAllTask;
    }

    //  await keyword knows this method, compiles to iterate
    public Awaiter GetAwaiter() => new(this);
}

public readonly struct Awaiter(MyTask task) : INotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;
    public void GetResult() => task.Wait();
    public Awaiter GetAwaiter() => this;
    public void OnCompleted(Action continuation) => task.ContinueWith(continuation);
}