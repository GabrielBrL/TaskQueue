namespace QueueTasks;

public class TaskQueue
{
    private readonly Queue<(Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId, string description)> _workItems = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _workItemsRunnign = new();
    private readonly SemaphoreSlim _signal = new(0);

    // Adiciona uma tarefa à fila com uma descrição e um CancellationTokenSource
    public Guid Enqueue(Func<CancellationToken, Task> workItem, string description)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));

        var cts = new CancellationTokenSource();
        var taskId = Guid.NewGuid(); // Identificador único para a tarefa

        lock (_workItems)
        {
            _workItems.Enqueue((workItem, cts, taskId, description));
            _signal.Release();
        }

        return taskId; // Retorna o ID da tarefa
    }

    // Pega a próxima tarefa da fila
    public async Task<(Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId)> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken); // Espera até que haja uma tarefa disponível

        lock (_workItems)
        {
            var (workItem, cts, taskId, _) = _workItems.Dequeue();
            return (workItem, cts, taskId); // Retorna a tarefa e seu CancellationTokenSource
        }
    }

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

    // Lista as descrições das tarefas enfileiradas
    public List<KeyValuePair<Guid, string>> GetPendingTasks()
    {
        lock (_workItems)
        {
            return _workItems.Select(w => new KeyValuePair<Guid, string>(w.taskId, w.description)).ToList();
        }
    }

    public List<Guid> GetRunningTasks()
    {
        lock (_workItemsRunnign)
        {
            return _workItemsRunnign.Select(w => w.Key).ToList();
        }
    }

    // Cancela uma tarefa específica
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
