# Enqueue Tasks

#### This package was create to enqueue tasks for running.

You can set a queue using...

```c#
_taskQueue.Enqueue(async token =>{
  
  //Task for example
  await Task.Delay(1000, token)

} , taskDescription)
```

You can execute a task using...

```c#
  var taskId = _taskQueue.Enqueue(async token =>
  {
      await Task.Delay(10000, token);      
  }, taskDescription);

  await _taskQueue.ExecuteSpecificTask(taskId);
```

You can delete a task running or in enqueue using...

```c#
  var taskId = _taskQueue.Enqueue(async token =>
  {
      await Task.Delay(10000, token);      
  }, taskDescription);

  //Invoked two functions to simute a situation where we executed a function and after we want to delete it
  Parallel.Invoke(
      async () => await _taskQueue.ExecuteSpecificTask(taskId),
      () => _taskQueue.DeleteTaskRunning(taskId)
  );
```

## Install

```bash
  dotnet add package QueueTasks
```
    
## Author

- [@GabrielBrl](https://github.com/GabrielBrL)


## Licence

[MIT](https://choosealicense.com/licenses/mit/)

