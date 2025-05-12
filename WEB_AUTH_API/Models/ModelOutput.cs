using Microsoft.ML.Data;

namespace WEB_AUTH_API.Models
{
    public class ModelOutput
    {
        // The predicted label: true = genuine, false = anomaly
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        // The probability/confidence of the positive class
        public float Probability { get; set; }

        // The raw score
        public float Score { get; set; }
    }
}
