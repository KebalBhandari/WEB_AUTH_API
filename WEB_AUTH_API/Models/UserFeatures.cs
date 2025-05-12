using Microsoft.ML.Data;

public class UserFeatures
{
    public int UserId { get; set; }
    public int AttemptNumber { get; set; }
    public int DataId { get; set; }

    // Behavioral biometric features
    public float TimingInterval { get; set; }
    public float KeyHoldDuration { get; set; }
    public float KeydownTime { get; set; }
    public float KeyupTime { get; set; }
    public float DotReactionTime { get; set; }
    public float ShapeReactionTime { get; set; }
    public float ShapeAccuracy { get; set; }
    public float MouseVelocity { get; set; }
    public float BackspacePress { get; set; }
    public float BackspaceInterval { get; set; }

    // Label for supervised training
    [ColumnName("Label")]
    public bool Label { get; set; }

    public string DetectedLanguage { get; set; }

    public UserFeatures() { }

    public UserFeatures(double[] featureArray)
    {
        if (featureArray.Length != 10)
            throw new ArgumentException("Feature array must contain exactly 10 elements.");

        TimingInterval = (float)featureArray[0];
        KeyHoldDuration = (float)featureArray[1];
        KeydownTime = (float)featureArray[2];
        KeyupTime = (float)featureArray[3];
        DotReactionTime = (float)featureArray[4];
        ShapeReactionTime = (float)featureArray[5];
        ShapeAccuracy = (float)featureArray[6];
        MouseVelocity = (float)featureArray[7];
        BackspacePress = (float)featureArray[8];
        BackspaceInterval = (float)featureArray[9];
    }

    public double[] ToArray()
    {
        return new double[]
        {
                TimingInterval,
                KeyHoldDuration,
                KeydownTime,
                KeyupTime,
                DotReactionTime,
                ShapeReactionTime,
                ShapeAccuracy,
                MouseVelocity,
                BackspacePress,
                BackspaceInterval
        };
    }
}