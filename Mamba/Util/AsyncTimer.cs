using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enklu.Mycelium.Util
{
    public class AsyncTimer : IDisposable
    {
        private readonly Action _callback;
        private readonly TimeSpan _delay;

        private CancellationTokenSource _cancel;

        public AsyncTimer(Action callback, TimeSpan delay)
        {
            _callback = callback;
            _delay = delay;
        }

        public void Start()
        {
            _cancel = new CancellationTokenSource();

            // Don't await here -- allow the cancellation token to stop the loop
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            InternalRun(_cancel.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public void Stop()
        {
            _cancel?.Cancel(false);
            _cancel = null;
        }

        private async Task InternalRun(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                _callback();

                await Task.Delay(_delay, cancel);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}