using Hangfire;
using Hangfire.Annotations;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using NCrontab;
using System;
using System.Collections.Generic;

namespace CustomBackgroundProcess
{
    public class RecurringJobManager
    {
        private readonly JobStorage _storage;
        private readonly IBackgroundJobFactory _factory;

        public RecurringJobManager()
            : this(JobStorage.Current)
        {
        }

        public RecurringJobManager([NotNull] JobStorage storage)
            : this(storage, new BackgroundJobFactory())
        {
        }

        public RecurringJobManager([NotNull] JobStorage storage, [NotNull] IBackgroundJobFactory factory)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _storage = storage;
            _factory = factory;
        }

        public void AddOrUpdate(
            [NotNull] string recurringJobId,
            [NotNull] Job job,
            [NotNull] string cronExpression)
        {
            AddOrUpdate(recurringJobId, job, cronExpression, TimeZoneInfo.Utc);
        }

        public void AddOrUpdate(
            [NotNull] string recurringJobId,
            [NotNull] Job job,
            [NotNull] string cronExpression,
            [NotNull] TimeZoneInfo timeZone)
        {
            AddOrUpdate(recurringJobId, job, cronExpression, timeZone, EnqueuedState.DefaultQueue);
        }

        public void AddOrUpdate(
            [NotNull] string recurringJobId,
            [NotNull] Job job,
            [NotNull] string cronExpression,
            [NotNull] TimeZoneInfo timeZone,
            [NotNull] string queue)
        {
            if (recurringJobId == null) throw new ArgumentNullException(nameof(recurringJobId));
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (cronExpression == null) throw new ArgumentNullException(nameof(cronExpression));
            if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));

            ValidateCronExpression(cronExpression);

            using (var connection = _storage.GetConnection())
            {
                var recurringJob = new Dictionary<string, string>();
                var invocationData = InvocationData.Serialize(job);

                recurringJob["Job"] = JobHelper.ToJson(invocationData);
                recurringJob["Cron"] = cronExpression;
                recurringJob["TimeZoneId"] = timeZone.Id;
                recurringJob["Queue"] = queue;

                using (var transaction = connection.CreateWriteTransaction())
                {
                    transaction.SetRangeInHash(
                        $"dc-recurring-job:{recurringJobId}",
                        recurringJob);

                    transaction.AddToSet("dc-recurring-jobs", recurringJobId);

                    transaction.Commit();
                }
            }
        }

        public void Trigger([NotNull] string recurringJobId)
        {
            if (recurringJobId == null) throw new ArgumentNullException(nameof(recurringJobId));

            using (var connection = _storage.GetConnection())
            {
                var hash = connection.GetAllEntriesFromHash($"dc-recurring-job:{recurringJobId}");
                if (hash == null)
                {
                    return;
                }

                var job = JobHelper.FromJson<InvocationData>(hash["Job"]).Deserialize();
                var state = new EnqueuedState { Reason = "Triggered using dc recurring job manager" };

                if (hash.ContainsKey("Queue"))
                {
                    state.Queue = hash["Queue"];
                }

                _factory.Create(new CreateContext(_storage, connection, job, state));
            }
        }

        public void RemoveIfExists([NotNull] string recurringJobId)
        {
            if (recurringJobId == null) throw new ArgumentNullException(nameof(recurringJobId));

            using (var connection = _storage.GetConnection())
            using (var transaction = connection.CreateWriteTransaction())
            {
                transaction.RemoveHash($"dc-recurring-job:{recurringJobId}");
                transaction.RemoveFromSet("dc-recurring-jobs", recurringJobId);

                transaction.Commit();
            }
        }

        private static void ValidateCronExpression(string cronExpression)
        {
            try
            {
                var schedule = CrontabSchedule.Parse(cronExpression,
                    new CrontabSchedule.ParseOptions() {IncludingSeconds = true});
                schedule.GetNextOccurrence(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("CRON expression is invalid. Please see the inner exception for details.", nameof(cronExpression), ex);
            }
        }
    }
}