using Newtonsoft.Json;

namespace WEB_AUTH_API.Models
{
    public class UserDataModel
    {
        public string TokenNo { get; set; }

        public List<List<double>> Timings { get; set; } = new List<List<double>>();

        public List<List<KeyHoldTime>> KeyHoldTimes { get; set; } = new List<List<KeyHoldTime>>();

        public List<List<BackSpaceTiming>> BackSpaceTimings { get; set; } = new List<List<BackSpaceTiming>>();

        public List<List<double>> DotTimings { get; set; } = new List<List<double>>();

        public List<List<ShapeTiming>> ShapeTimings { get; set; } = new List<List<ShapeTiming>>();

        public List<List<MouseMovement>> ShapeMouseMovements { get; set; } = new List<List<MouseMovement>>();

        public List<string> DetectedLanguages { get; set; } = new List<string>();

    }

    public class BackSpaceTiming
    {
        public double Time { get; set; }
        public string Action { get; set; }
    }
}
