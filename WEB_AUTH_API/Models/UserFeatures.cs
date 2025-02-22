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
        public float BackspacePressCount { get; set; }
        public float AvgBackspaceInterval { get; set; }

        // Constructor to initialize from a double array
        public UserFeatures(double[] featureArray)
        {
            if (featureArray.Length != 8)
                throw new ArgumentException("Feature array must contain exactly 8 elements.");

            AvgTimingInterval = (float)featureArray[0];
            StdDevTimingInterval = (float)featureArray[1];
            AvgKeyHoldDuration = (float)featureArray[2];
            StdDevKeyHoldDuration = (float)featureArray[3];
            AvgDotReactionTime = (float)featureArray[4];
            AvgShapeReactionTime = (float)featureArray[5];
            ShapeAccuracy = (float)featureArray[6];
            AvgMouseSpeed = (float)featureArray[7];
            BackspacePressCount = (float)featureArray[8]; 
            AvgBackspaceInterval = (float)featureArray[9];
        }

        public double[] ToArray()
        {
            return new double[]
            {
            AvgTimingInterval,
            StdDevTimingInterval,
            AvgKeyHoldDuration,
            StdDevKeyHoldDuration,
            AvgDotReactionTime,
            AvgShapeReactionTime,
            ShapeAccuracy,
            AvgMouseSpeed,
            BackspacePressCount,
            AvgBackspaceInterval,
            };
        }

        // Parameterless constructor for ML.NET
        public UserFeatures() { }
    }


}
