using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnVoQbot.MultiUpdateCommandsStagesEnums
{
    internal enum EditWordStages
    {
        ChooseWordToEdit,
        ChooseWhatToEdit, //spelling or translation
        SendDataRequest,
        GetNewData
    }
}
