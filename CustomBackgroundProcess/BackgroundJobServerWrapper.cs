using Hangfire;
using Hangfire.Server;
using System.Collections.Concurrent;
using System.Linq;

namespace CustomBackgroundProcess
{
    public class BackgroundJobServerWrapper
    {
        private static readonly ConcurrentBag<BackgroundJobServer> Servers
            = new ConcurrentBag<BackgroundJobServer>();

        public BackgroundJobServerWrapper(BackgroundJobServerOptions options, JobStorage storage,
            params IBackgroundProcess[] addtionaProcesses)
        {
            if (addtionaProcesses.All(x => x.GetType() != typeof (RecurringJobScheduler)))
            {
                addtionaProcesses =
                    addtionaProcesses.Concat(new[] {new RecurringJobScheduler()}).ToArray();
            }

            var server = new BackgroundJobServer(options, storage, addtionaProcesses);

            Servers.Add(server);
        }
    }
}