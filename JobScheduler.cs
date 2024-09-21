using EnVoQbot.AdditionalObjects;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;

namespace EnVoQbot
{
    internal static class JobScheduler
    {
        internal static async Task<IScheduler> SetUp()
        {
            NameValueCollection configurationProperties = new NameValueCollection()
            {
                ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
                ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
                ["quartz.jobStore.tablePrefix"] = "QRTZ_",
                ["quartz.jobStore.dataSource"] = "myDS",
                ["quartz.serializer.type"] = "json",
                ["quartz.jobStore.useProperties"] = "true",

                ["quartz.dataSource.myDS.connectionString"] = ConnectionsData.QuartzDBconnectionString,
                ["quartz.dataSource.myDS.provider"] = "SqlServer"
            };
            var schedulerFactory = new StdSchedulerFactory(configurationProperties);
            return await schedulerFactory.GetScheduler();
        }
    }
}
