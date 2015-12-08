using Hangfire;
using Hangfire.Annotations;
using Hangfire.Server;

namespace CustomBackgroundProcess
{
    public static class GlobalConfigurationExtensions
    {
        public static IGlobalConfiguration UseHangfireServer([NotNull] this IGlobalConfiguration configuration)
        {
            return configuration.UseHangfireServer(JobStorage.Current, new BackgroundJobServerOptions());
        }

        public static IGlobalConfiguration UseHangfireServer([NotNull] this IGlobalConfiguration configuration,
           [NotNull] BackgroundJobServerOptions options,
           [NotNull] params IBackgroundProcess[] additionalProcesses)
        {

            return configuration.UseHangfireServer(JobStorage.Current, options, additionalProcesses);
        }

        public static IGlobalConfiguration UseHangfireServer([NotNull] this IGlobalConfiguration configuration,
           [NotNull] params IBackgroundProcess[] additionalProcesses)
        {

            return configuration.UseHangfireServer(JobStorage.Current, new BackgroundJobServerOptions(), additionalProcesses);
        }

        public static IGlobalConfiguration UseHangfireServer([NotNull] this IGlobalConfiguration configuration,
            [NotNull] JobStorage storage,
            [NotNull] BackgroundJobServerOptions options,
            [NotNull] params IBackgroundProcess[] additionalProcesses)
        {
            var server = new BackgroundJobServerWrapper(options, storage, additionalProcesses);

            return configuration;
        }
    }
}