using System;
using System.Threading;

namespace CustomBackgroundProcess
{
    internal class EverySecondThrottler : IThrottler
    {
        public void Throttle(CancellationToken token)
        {
            while (DateTime.Now.Millisecond != 0)
            {
                WaitAMillisecondOrThrowIfCanceled(token);
            }
            WaitAMillisecondOrThrowIfCanceled(token);
        }

        public void Delay(CancellationToken token)
        {
            WaitAMillisecondOrThrowIfCanceled(token);
        }

        private static void WaitAMillisecondOrThrowIfCanceled(CancellationToken token)
        {
            token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1));
            token.ThrowIfCancellationRequested();
        }
    }
}