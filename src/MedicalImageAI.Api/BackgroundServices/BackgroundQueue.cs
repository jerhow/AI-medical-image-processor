using System.Threading.Channels;
using MedicalImageAI.Api.BackgroundServices.Interfaces;

namespace MedicalImageAI.Api.BackgroundServices;

public class BackgroundQueue<T> : IBackgroundQueue<T>
{
    private readonly Channel<T> _queue;

    public BackgroundQueue(int capacity)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait // Wait if queue is full
        };
        _queue = Channel.CreateBounded<T>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(T workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<T> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}
