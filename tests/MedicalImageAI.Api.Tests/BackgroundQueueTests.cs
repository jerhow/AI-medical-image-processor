using MedicalImageAI.Api.BackgroundServices;

namespace MedicalImageAI.Api.Tests;

public class BackgroundQueueTests
{
    /// <summary>
    /// Ensure that `QueueBackgroundWorkItemAsync` correctly handles invalid input, like a null workItem. 
    /// It should throw an `ArgumentNullException`.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task QueueBackgroundWorkItemAsync_NullItem_ThrowsArgumentNullException()
    {
        // Arrange
        // For this test, the type of T doesn't really matter, so we'll use string for clarity. Capacity also doesn't matter much here.
        var queue = new BackgroundQueue<string>(capacity: 1);
        string? nullWorkItem = null;

        // Act & Assert
        // The action that should throw the exception is passed as a lambda.
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await queue.QueueBackgroundWorkItemAsync(nullWorkItem!)
        );
    }

    /// <summary>
    /// Enqueue an item, then dequeue it, and verify it's the same item.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task EnqueueAsync_Then_DequeueAsync_ShouldReturnSameItem()
    {
        // Arrange
        var queue = new BackgroundQueue<string>(capacity: 1); // Capacity of 1 is fine for this
        string testItem = "hello_queue_world";

        // Act
        // Enqueue the item
        await queue.QueueBackgroundWorkItemAsync(testItem);

        // Dequeue the item
        // CancellationToken.None is used when we're not specifically testing cancellation.
        var dequeuedItem = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(dequeuedItem);         // Ensure we got something back
        Assert.Equal(testItem, dequeuedItem); // Ensure it's the same item we enqueued
    }

    /// <summary>
    /// Enqueue multiple items, dequeue them, and verify FIFO order.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task EnqueueMultiple_DequeueMultiple_ShouldPreserveFifoOrder()
    {
        // Arrange
        var queue = new BackgroundQueue<string>(capacity: 5); // Capacity to hold all items
        string item1 = "first_item";
        string item2 = "second_item";
        string item3 = "third_item";

        // Act
        // Enqueue items in a specific order
        await queue.QueueBackgroundWorkItemAsync(item1);
        await queue.QueueBackgroundWorkItemAsync(item2);
        await queue.QueueBackgroundWorkItemAsync(item3);

        // Dequeue items
        var dequeuedItem1 = await queue.DequeueAsync(CancellationToken.None);
        var dequeuedItem2 = await queue.DequeueAsync(CancellationToken.None);
        var dequeuedItem3 = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        // Verify they were dequeued in the same order they were enqueued
        Assert.Equal(item1, dequeuedItem1);
        Assert.Equal(item2, dequeuedItem2);
        Assert.Equal(item3, dequeuedItem3);
    }

    /// <summary>
    /// `DequeueAsync` throws `TaskCanceledException` when the queue is empty and the token is canceled.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task DequeueAsync_WhenQueueIsEmptyAndTokenIsCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var queue = new BackgroundQueue<string>(capacity: 1); // Queue is initially empty
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        cts.Cancel(); // Cancel the token *before* calling DequeueAsync

        // Assert
        // Verify that DequeueAsync throws OperationCanceledException when called with a canceled token
        // on an empty queue.
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await queue.DequeueAsync(token)
        );
    }

    /// <summary>
    /// `DequeueAsync` waits if queue is empty, then completes when item is added.
    /// This test ensures that if the queue is empty, it will wait for an item to be enqueued.
    /// It uses a timeout to ensure the test doesn't hang indefinitely.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task DequeueAsync_WhenQueueIsEmpty_ShouldWaitUntilItemIsAvailable()
    {
        // Arrange
        var queue = new BackgroundQueue<string>(capacity: 1);
        string testItem = "waiting_item";
        var dequeueTimeout = TimeSpan.FromSeconds(2); // Safety timeout for the test

        // Act
        // Start DequeueAsync on the empty queue, it returns a ValueTask<string>
        var dequeueValueTask = queue.DequeueAsync(CancellationToken.None);

        // Assert (optional intermediate check): The ValueTask should not be completed yet.
        // Give it a very brief moment to ensure it has started waiting if it's truly async.
        await Task.Delay(50); 
        Assert.False(dequeueValueTask.IsCompleted, "DequeueAsync (ValueTask) should be waiting for an item.");

        // Now, enqueue an item
        await queue.QueueBackgroundWorkItemAsync(testItem);

        // Convert the ValueTask<string> to a Task<string> for use with Task.WhenAny
        var dequeueTask = dequeueValueTask.AsTask();

        // The dequeueTask (which is a Task<string>) should complete.
        // We await it with a timeout using Task.WhenAny.
        var completedTask = await Task.WhenAny(dequeueTask, Task.Delay(dequeueTimeout));

        // Assert
        Assert.Equal(dequeueTask, completedTask); // Ensure it wasn't the timeout
        Assert.True(dequeueTask.IsCompletedSuccessfully, "DequeueAsync task should have completed successfully.");
        
        // Get the result by awaiting the (already completed) task
        var actualDequeuedItem = await dequeueTask;
        Assert.Equal(testItem, actualDequeuedItem);
    }
}
