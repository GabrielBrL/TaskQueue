using Microsoft.Extensions.Hosting;

namespace QueueTasks;

internal class TaskProcessing : BackgroundService
{
    private readonly TaskQueue _taskQueue;

    public TaskProcessing(TaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (workItem, cts, taskId) = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                // Executa a tarefa com seu próprio CancellationToken
                await workItem(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Tarefa foi cancelada.");
            }
            catch (Exception ex)
            {
                // Tratar exceções conforme necessário
                Console.WriteLine($"Erro ao processar tarefa: {ex.Message}");
            }
        }
    }
}