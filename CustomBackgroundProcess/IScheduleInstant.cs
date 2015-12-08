using System;
using System.Collections.Generic;

namespace CustomBackgroundProcess
{
    internal interface IScheduleInstant
    {
        DateTime NowInstant { get; }
        DateTime NextInstant { get; }
        IEnumerable<DateTime> GetNextInstants(DateTime? lastInstant);
    }
}