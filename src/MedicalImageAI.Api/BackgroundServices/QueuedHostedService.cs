using MedicalImageAI.Api.BackgroundServices.Interfaces;

namespace MedicalImageAI.Api.BackgroundServices;

    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> _taskQueue;
        private readonly IServiceProvider _serviceProvider; // To create DI scopes

        public QueuedHostedService(
            IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> taskQueue,
            ILogger<QueuedHostedService> logger,
            IServiceProvider serviceProvider)
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName} is running.", nameof(QueuedHostedService));
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for a work item
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    // Create a short-lived, self-disposing DI scope for this work item, to resolve scoped services (like access to CustomVisionService, or a database context)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        _logger.LogInformation("Dequeued work item. Executing...");
                        
                        await workItem(scope.ServiceProvider, stoppingToken); // Execute the work item delegate, passing the scoped service provider
                        _logger.LogInformation("Work item execution finished.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if stoppingToken was signaled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing work item.");
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName} is stopping.", nameof(QueuedHostedService));
            await base.StopAsync(stoppingToken);
        }
    }
