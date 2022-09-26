namespace Common.Outbox.Base;

public class DelayableTimer
{
    private readonly PeriodicTimer _timer;
    private bool _delayed;

    public DelayableTimer(TimeSpan interval)
    {
        _timer = new PeriodicTimer(interval);
    }

    public async Task WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        do
        {
            _delayed = false;
            await _timer.WaitForNextTickAsync(cancellationToken);
        } while (_delayed && !cancellationToken.IsCancellationRequested);
    }

    public void Delay()
    {
        _delayed = true;
    }
}