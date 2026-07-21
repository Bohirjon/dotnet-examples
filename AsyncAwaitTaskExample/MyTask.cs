using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncAwaitTaskExample;

//  async keyword knows this attribute, compiler builds the state machine
[AsyncMethodBuilder(typeof(MyTaskMethodBuilder))]
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
        if (IsCompleted)
            throw new Exception("Task is completed");

        _isCompleted = true;
        _exception = exception;

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

    public static MyTask Delay(int duration)
    {
        var task = new MyTask();

        _ = new Timer(_ => task.SetResult()).Change(duration, -1);

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

public struct MyTaskMethodBuilder
{
    public static MyTaskMethodBuilder Create() => new() { Task = new MyTask() };

    public MyTask Task { get; private set; }

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        var executionContext = ExecutionContext.Capture();
        try
        {
            stateMachine.MoveNext();
        }
        finally
        {
            if (executionContext is not null)
                ExecutionContext.Restore(executionContext);
        }
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
    }

    //  called when the async method body finishes
    public void SetResult() => Task.SetResult();
    public void SetException(Exception exception) => Task.SetException(exception);

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => awaiter.OnCompleted(stateMachine.MoveNext);

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => AwaitOnCompleted(ref awaiter, ref stateMachine);
}