namespace QueueTasks;

public class TaskQueue
{
    private readonly Queue<(Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId, string description)> _workItems = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _workItemsRunnign = new();
    private readonly SemaphoreSlim _signal = new(0);

    /// <summary>
    /// Enqueue the tasks
    /// </summary>
    /// <param name="workItem">Function that will be enqueue</param>
    /// <param name="description">A little description to save what will execute</param>
    /// <returns>Guid of the task</returns>
    public Guid Enqueue(Func<CancellationToken, Task> workItem, string description)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));

        var cts = new CancellationTokenSource();
        var taskId = Guid.NewGuid(); // Identificador único para a tarefa

        lock (_workItems)
        {
            _workItems.Enqueue((workItem, cts, taskId, description));            
        }

        return taskId;
    }

    /// <summary>
    /// Dequeue the tasks
    /// </summary>
    /// <param name="cancellationToken">Function that will be take off from the queue</param>    
    /// <returns>
    /// - Function from the queue
    /// - CancellationTokenSource
    /// - Guid of the task
    /// </returns>
    public async Task<(Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId)> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken); // Espera até que haja uma tarefa disponível

        lock (_workItems)
        {
            var (workItem, cts, taskId, _) = _workItems.Dequeue();
            return (workItem, cts, taskId); // Retorna a tarefa e seu CancellationTokenSource
        }
    }

    /// <summary>
    /// Execute specific task by Guid
    /// </summary>
    /// <param name="taskId">Function Guid</param>    
    /// <returns>
    /// Return if the task was found and executed
    /// </returns>
    public async Task<bool> ExecuteSpecificTask(Guid taskId)
    {
        (Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId, string description) taskExecuted;
        lock (_workItems)
        {
            var taskList = _workItems.ToList();
            taskExecuted = taskList.FirstOrDefault(x => x.taskId == taskId);

            if (taskExecuted.Equals(default((Func<CancellationToken, Task>, CancellationTokenSource, Guid))))
                return false; // Tarefa não encontrada            


            taskList.Remove(taskExecuted);
            _workItems.Clear();

            foreach (var task in taskList)
            {
                _workItems.Enqueue(task);
            }
        }
        lock (_workItemsRunnign)
        {
            _workItemsRunnign[taskId] = taskExecuted.cts;
        }

        try
        {
            await taskExecuted.workItem(taskExecuted.cts.Token);
        }
        finally
        {
            lock (_workItemsRunnign)
            {
                _workItemsRunnign.Remove(taskId);
            }
        }
        return true;
    }

    /// <summary>
    /// Get the pending tasks list
    /// </summary>
    /// <returns>Return a list containing the pending tasks</returns>
    public List<KeyValuePair<Guid, string>> GetPendingTasks()
    {
        lock (_workItems)
        {
            return _workItems.Select(w => new KeyValuePair<Guid, string>(w.taskId, w.description)).ToList();
        }
    }
    /// <summary>
    /// Get the running tasks list
    /// </summary>
    /// <returns>Return a list containing the running tasks</returns>
    public List<Guid> GetRunningTasks()
    {
        lock (_workItemsRunnign)
        {
            return _workItemsRunnign.Select(w => w.Key).ToList();
        }
    }

    /// <summary>
    /// Cancel a task running
    /// </summary>
    /// <param name="taskId">Function Guid</param>
    /// <returns>If the task was found and canceled</returns>
    public bool CancelTask(Guid taskId)
    {
        lock (_workItems)
        {
            var task = _workItems.FirstOrDefault(t => t.cts.Token.CanBeCanceled && t.taskId == taskId);
            if (task == default) return false; // Se não encontrou a tarefa

            task.cts.Cancel(); // Cancela a tarefa            
            return true; // Tarefa encontrada e cancelada
        }
    }
    /// <summary>
    /// Delete a task enqueued
    /// </summary>
    /// <param name="taskId">Function Guid</param>
    /// <returns>If the task was found and deleted</returns>
    public bool DeleteTaskInQueue(Guid taskId)
    {
        lock (_workItems)
        {
            var taskList = _workItems.ToList();
            var taskRemoved = taskList.FirstOrDefault(x => x.taskId == taskId);

            if (taskRemoved.Equals(default((Func<CancellationToken, Task>, CancellationTokenSource, Guid))))
                return false; // Tarefa não encontrada            

            taskList.Remove(taskRemoved);
            _workItems.Clear();

            foreach (var task in taskList)
            {
                _workItems.Enqueue(task);
            }

            return true;
        }
    }
    /// <summary>
    /// Delete a task running
    /// </summary>
    /// <param name="taskId">Function Guid</param>
    /// <returns>If the task was found and deleted</returns>
    public bool DeleteTaskRunning(Guid taskId)
    {
        lock (_workItemsRunnign)
        {
            if (_workItemsRunnign.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                var task = _workItems.FirstOrDefault(x => x.taskId == taskId);
                var listWorkItems = _workItems.ToList();
                _workItems.Clear();

                listWorkItems.Remove(task);

                foreach (var item in listWorkItems)
                {
                    _workItems.Enqueue(item);
                }
                return true;
            }
        }
        return false;
    }
}
