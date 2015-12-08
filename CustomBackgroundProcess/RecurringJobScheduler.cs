using Hangfire;
using Hangfire.Annotations;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using NCrontab;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBackgroundProcess
{
    public class RecurringJobScheduler : IBackgroundProcess
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1);
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        private readonly IBackgroundJobFactory _factory;
        private readonly Func<CrontabSchedule, TimeZoneInfo, IScheduleInstant> _instantFactory;
        private readonly IThrottler _throttler;

        public RecurringJobScheduler()
            : this(new BackgroundJobFactory())
        {
        }

        public RecurringJobScheduler([NotNull] IBackgroundJobFactory factory)
            : this(factory, ScheduleInstant.Factory, new EverySecondThrottler())
        {
        }

        internal RecurringJobScheduler(
            [NotNull] IBackgroundJobFactory factory,
            [NotNull] Func<CrontabSchedule, TimeZoneInfo, IScheduleInstant> instantFactory,
            [NotNull] IThrottler throttler)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (instantFactory == null) throw new ArgumentNullException(nameof(instantFactory));
            if (throttler == null) throw new ArgumentNullException(nameof(throttler));

            _factory = factory;
            _instantFactory = instantFactory;
            _throttler = throttler;
        }

        /// <inheritdoc />
        public void Execute(BackgroundProcessContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _throttler.Throttle(context.CancellationToken);

            using (var connection = context.Storage.GetConnection())
            using (connection.AcquireDistributedLock("dc-recurring-jobs:lock", LockTimeout))
            {
                var recurringJobIds = connection.GetAllItemsFromSet("dc-recurring-jobs");

                foreach (var recurringJobId in recurringJobIds)
                {
                    var recurringJob = connection.GetAllEntriesFromHash(
                        $"dc-recurring-job:{recurringJobId}");

                    if (recurringJob == null)
                    {
                        continue;
                    }

                    try
                    {
                        TryScheduleJob(context.Storage, connection, recurringJobId, recurringJob);
                    }
                    catch (JobLoadException ex)
                    {
                        Logger.WarnException(
                            $"Recurring job '{recurringJobId}' can not be scheduled due to job load exception.",
                            ex);
                    }
                }

                _throttler.Delay(context.CancellationToken);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return GetType().Name;
        }

        private void TryScheduleJob(
            JobStorage storage,
            IStorageConnection connection,
            string recurringJobId,
            IReadOnlyDictionary<string, string> recurringJob)
        {
            var serializedJob = JobHelper.FromJson<InvocationData>(recurringJob["Job"]);
            var job = serializedJob.Deserialize();
            var cron = recurringJob["Cron"];
            var cronSchedule = CrontabSchedule.Parse(cron, new CrontabSchedule.ParseOptions() { IncludingSeconds = true });

            try
            {
                var timeZone = recurringJob.ContainsKey("TimeZoneId")
                    ? TimeZoneInfo.FindSystemTimeZoneById(recurringJob["TimeZoneId"])
                    : TimeZoneInfo.Utc;

                var instant = _instantFactory(cronSchedule, timeZone);

                var lastExecutionTime = recurringJob.ContainsKey("LastExecution")
                    ? JobHelper.DeserializeDateTime(recurringJob["LastExecution"])
                    : (DateTime?)null;

                var changedFields = new Dictionary<string, string>();

                if (instant.GetNextInstants(lastExecutionTime).Any())
                {
                    var state = new EnqueuedState { Reason = "Triggered by dc recurring job scheduler" };
                    if (recurringJob.ContainsKey("Queue") && !String.IsNullOrEmpty(recurringJob["Queue"]))
                    {
                        state.Queue = recurringJob["Queue"];
                    }

                    var backgroundJob = _factory.Create(new CreateContext(storage, connection, job, state));
                    var jobId = backgroundJob != null ? backgroundJob.Id : null;

                    if (String.IsNullOrEmpty(jobId))
                    {
                        Logger.DebugFormat(
                            "Recurring job '{0}' execution at '{1}' has been canceled.",
                            recurringJobId,
                            instant.NowInstant);
                    }

                    changedFields.Add("LastExecution", JobHelper.SerializeDateTime(instant.NowInstant));
                    changedFields.Add("LastJobId", jobId ?? String.Empty);
                }

                changedFields.Add("NextExecution", JobHelper.SerializeDateTime(instant.NextInstant));

                connection.SetRangeInHash(
                    $"dc-recurring-job:{recurringJobId}",
                    changedFields);
            }
            catch (TimeZoneNotFoundException ex)
            {
                Logger.ErrorException(
                    $"DC Recurring job '{recurringJobId}' was not triggered: {ex.Message}.",
                    ex);
            }
        }
    }
}