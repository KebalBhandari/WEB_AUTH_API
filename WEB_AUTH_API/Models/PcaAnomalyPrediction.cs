using Microsoft.ML.Data;

namespace WEB_AUTH_API.Models
{
    public class PcaAnomalyPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool IsAnomaly { get; set; }

        public float Score { get; set; }

    }
}
