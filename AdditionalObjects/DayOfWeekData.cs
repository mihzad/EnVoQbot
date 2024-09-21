namespace EnVoQbot.AdditionalObjects
{
    internal class DayOfWeekScheduleData
    {
        internal DayOfWeekScheduleData(string dayname, int indexInSequence)
        {
            keyboardButtonText = dayname;
            dayName = dayname;
            IndexInSequence = indexInSequence;
        }

        internal int IndexInSequence { get; set; }

        internal string keyboardButtonText;
        internal bool isDayScheduled = false;
        internal string? timerData;
        internal string dayName;
        internal int QuizzesCount {
            get { return quizzesCount; }
            set {
                if (value > 100)
                    quizzesCount = 100;
                else if(value < 1)
                    quizzesCount = 1;
                else
                    quizzesCount = value;
            }
        }
        private int quizzesCount = 1;
        internal void ChangeStatus()
        {
            if (isDayScheduled == false) {
                keyboardButtonText = dayName + "(chosen)";
                isDayScheduled = true;
            }
            else {
                keyboardButtonText = dayName;
                isDayScheduled = false;
            }       
        }
    }
}
