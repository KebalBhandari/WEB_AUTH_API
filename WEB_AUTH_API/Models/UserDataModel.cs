namespace WEB_AUTH_API.Models
{
    public class UserDataModel
    {
        public string TokenNo { get; set; }
        public List<List<double>> Timings { get; set; }

        public List<List<KeyHoldTime>> KeyHoldTimes { get; set; }

        public List<List<double>> DotTimings { get; set; }

        public List<List<ShapeTiming>> ShapeTimings { get; set; }

        public List<List<MouseMovement>> ShapeMouseMovements { get; set; }

    }
}
