using Hangfire.Annotations;
using NCrontab;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBackgroundProcess
{
    internal class ScheduleInstant : IScheduleInstant
    {
        private readonly TimeZoneInfo _timeZone;
        private readonly CrontabSchedule _schedule;

        public static Func<CrontabSchedule, TimeZoneInfo, IScheduleInstant> Factory =
            (schedule, timeZone) => new ScheduleInstant(DateTime.UtcNow, timeZone, schedule);

        public ScheduleInstant(DateTime nowInstant, TimeZoneInfo timeZone, [NotNull] CrontabSchedule schedule)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (nowInstant.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Only DateTime values in UTC should be passed.", nameof(nowInstant));
            }

            _timeZone = timeZone;
            _schedule = schedule;

            NowInstant = nowInstant.AddSeconds(-nowInstant.Second);
            NextInstant = TimeZoneInfo.ConvertTime(
                _schedule.GetNextOccurrence(TimeZoneInfo.ConvertTime(NowInstant, TimeZoneInfo.Utc, _timeZone)),
                _timeZone,
                TimeZoneInfo.Utc);
        }

        public DateTime NowInstant { get; private set; }
        public DateTime NextInstant { get; private set; }

        public IEnumerable<DateTime> GetNextInstants(DateTime? lastInstant)
        {
            if (lastInstant.HasValue && lastInstant.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Only DateTime values in UTC should be passed.", nameof(lastInstant));
            }

            var baseTime = lastInstant ?? NowInstant.AddSeconds(-1);
            var endTime = NowInstant.AddSeconds(1);

            return _schedule
                .GetNextOccurrences(
                    TimeZoneInfo.ConvertTimeFromUtc(baseTime, _timeZone),
                    TimeZoneInfo.ConvertTimeFromUtc(endTime, _timeZone))
                .Select(x => TimeZoneInfo.ConvertTimeToUtc(x, _timeZone))
                .ToList();
        }
    }
}