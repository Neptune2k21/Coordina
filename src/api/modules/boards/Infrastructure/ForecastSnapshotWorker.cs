using Coordina.Api.Modules.Boards.Application;

namespace Coordina.Api.Modules.Boards.Infrastructure;

/// <summary>
/// Records the forecasts of every board once a day. It looks every hour, so a
/// server that was down at midnight still records the day.
/// </summary>
internal sealed partial class ForecastSnapshotWorker(
  IServiceScopeFactory scopes,
  TimeProvider clock,
  ILogger<ForecastSnapshotWorker> logger) : BackgroundService
{
  private static readonly TimeSpan Period = TimeSpan.FromHours(1);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(Period, clock);

    do
    {
      await RecordAsync(stoppingToken);
    }
    while (await timer.WaitForNextTickAsync(stoppingToken));
  }

  // A failure is logged and tried again an hour later: forecasts missing a day
  // must not take the API down.
  private async Task RecordAsync(CancellationToken stoppingToken)
  {
    try
    {
      Guid? last = Guid.Empty;

      while (last is { } after)
      {
        await using var scope = scopes.CreateAsyncScope();
        last = await scope.ServiceProvider
          .GetRequiredService<ForecastSnapshotRecorder>()
          .RecordAsync(after, stoppingToken);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // The server is stopping.
    }
#pragma warning disable CA1031 // Whatever failed, the next hour tries again.
    catch (Exception exception)
#pragma warning restore CA1031
    {
      LogFailure(logger, exception);
    }
  }

  [LoggerMessage(LogLevel.Error, "Recording the forecasts of the day failed; trying again in an hour.")]
  private static partial void LogFailure(ILogger logger, Exception exception);
}
