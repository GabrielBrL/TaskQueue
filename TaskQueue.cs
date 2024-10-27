namespace QueueTasks;

public class TaskQueue
{
    private readonly Queue<(Func<CancellationToken, Task> workItem, CancellationTokenSource cts, Guid taskId, string description)> _workItems = new();
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
            _signal.Release(); // Libera o "sinal" para processar
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

    // Lista as descrições das tarefas enfileiradas
    public List<Guid> GetPendingTasks()
    {
        lock (_workItems)
        {
            return _workItems.Select(w => w.taskId).ToList();
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
}
