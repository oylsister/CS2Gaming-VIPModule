namespace VipItem
{
    public class PlayerData
    {
        public PlayerData(string achieve, string reset, bool claimed = false)
        {
            _timeAcheived = achieve;
            _timeReset = reset;
            _claimed = claimed;
        }

        private string _timeAcheived;
        private string _timeReset;
        private bool _claimed;

        public string TimeAcheived
        {
            get { return _timeAcheived; }
            set { _timeAcheived = value; }
        }

        public string TimeReset
        {
            get { return _timeReset; }
            set { _timeReset = value; }
        }

        public bool Claimed
        {
            get { return _claimed; }
            set { _claimed = value; }
        }
    }
}
