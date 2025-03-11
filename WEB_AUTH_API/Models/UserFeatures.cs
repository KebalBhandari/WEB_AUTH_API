namespace WEB_AUTH_API.Models
{
    public class UserFeatures
    {
        public int UserId { get; set; }
        public int AttemptNumber { get; set; }
        public int DataId { get; set; }                    // Unique identifier for each data point
        public float TimingInterval { get; set; }          // Single timing interval
        public float KeyHoldDuration { get; set; }         // Single key hold duration
        public float DotReactionTime { get; set; }         // Single dot reaction time
        public float ShapeReactionTime { get; set; }       // Single shape reaction time
        public float ShapeAccuracy { get; set; }           // 1 or 0 for correct/incorrect
        public float MouseVelocity { get; set; }           // Single mouse velocity
        public float BackspacePress { get; set; }          // 1 if pressed, 0 if not
        public float BackspaceInterval { get; set; }       // Time since last backspace
        public string DetectedLanguage { get; set; }       // Kept for reference, not used in PCA

        public UserFeatures(double[] featureArray)
        {
            if (featureArray.Length != 8) // Adjusted for numeric features used in PCA
                throw new ArgumentException("Feature array must contain exactly 8 elements.");

            TimingInterval = (float)featureArray[0];
            KeyHoldDuration = (float)featureArray[1];
            DotReactionTime = (float)featureArray[2];
            ShapeReactionTime = (float)featureArray[3];
            ShapeAccuracy = (float)featureArray[4];
            MouseVelocity = (float)featureArray[5];
            BackspacePress = (float)featureArray[6];
            BackspaceInterval = (float)featureArray[7];
        }

        public double[] ToArray()
        {
            return new double[]
            {
                TimingInterval,
                KeyHoldDuration,
                DotReactionTime,
                ShapeReactionTime,
                ShapeAccuracy,
                MouseVelocity,
                BackspacePress,
                BackspaceInterval
            };
        }

        // Parameterless constructor for ML.NET
        public UserFeatures() { }
    }


}
