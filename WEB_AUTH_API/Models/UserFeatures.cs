namespace WEB_AUTH_API.Models
{
    public class UserFeatures
    {
        public float AvgTimingInterval { get; set; }
        public float StdDevTimingInterval { get; set; }
        public float AvgKeyHoldDuration { get; set; }
        public float StdDevKeyHoldDuration { get; set; }
        public float AvgDotReactionTime { get; set; }
        public float AvgShapeReactionTime { get; set; }
        public float ShapeAccuracy { get; set; }
        public float AvgMouseSpeed { get; set; }
    }

}
