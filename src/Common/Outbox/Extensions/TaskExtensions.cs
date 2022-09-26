namespace Common.Outbox.Extensions;

public static class TaskExtensions
{
    public static Task ReturnExceptions(this Task<Task> task)
    {
        return task.ContinueWith(faultedTask =>
            {
                // The code will only reach here if any of the tasks were finished
                // By a cancellation or an exception
                if (faultedTask.Result.Exception is not null)
                    throw faultedTask.Result.Exception;

                if (faultedTask.Exception is not null)
                    throw faultedTask.Exception;
            },
            // The task has ended
            CancellationToken.None);
    }
}