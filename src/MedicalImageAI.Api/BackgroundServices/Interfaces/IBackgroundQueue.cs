namespace MedicalImageAI.Api.BackgroundServices.Interfaces;

public interface IBackgroundQueue<T> // The work item type here is a Func delegate
{
    ValueTask QueueBackgroundWorkItemAsync(T workItem);
    ValueTask<T> DequeueAsync(CancellationToken cancellationToken);
}
