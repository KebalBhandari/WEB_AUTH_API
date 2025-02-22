namespace WEB_AUTH_API.Models
{
    public class UserBehaviorDataModel
    {
        public int UserId { get; set; }

        public List<List<double>> Timings { get; set; } = new List<List<double>>();

        public List<List<KeyHoldTime>> KeyHoldTimes { get; set; } = new List<List<KeyHoldTime>>();

        public List<List<double>> DotTimings { get; set; } = new List<List<double>>();

        public List<List<ShapeTiming>> ShapeTimings { get; set; } = new List<List<ShapeTiming>>();

        public List<List<MouseMovement>> ShapeMouseMovements { get; set; } = new List<List<MouseMovement>>();

        public List<List<BackspaceTiming>> BackspaceTimings { get; set; } = new List<List<BackspaceTiming>>();

    }

    public class KeyHoldTime
    {
        public double Duration { get; set; }
    }

    public class ShapeTiming
    {
        public double ReactionTime { get; set; }
        public int IsCorrect { get; set; }
    }

    public class MouseMovement
    {
        public double Time { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

}
