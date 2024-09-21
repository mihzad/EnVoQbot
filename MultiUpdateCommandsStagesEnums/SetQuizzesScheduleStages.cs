using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnVoQbot.MultiUpdateCommandsStagesEnums
{
    internal enum SetQuizzesScheduleStages
    {
        SetUpChoosingKeyboard,
        ChooseDaysOfWeek,
        GetTimerForEachOfScheduledDays,
        GetQuizzesCountForEachOfScheduledDays,
        ScheduleJobs
    }
}
