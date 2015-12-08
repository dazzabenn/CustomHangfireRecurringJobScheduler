using System.Threading;

namespace CustomBackgroundProcess
{
    internal interface IThrottler
    {
        void Throttle(CancellationToken token);
        void Delay(CancellationToken token);
    }
}