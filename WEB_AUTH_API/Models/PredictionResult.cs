using Microsoft.ML.Data;

namespace WEB_AUTH_API.Models
{
    public class PredictionResult
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
