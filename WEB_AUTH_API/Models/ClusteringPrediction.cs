using Microsoft.ML.Data;

namespace WEB_AUTH_API.Models
{
    public class ClusteringPrediction
    {
        [ColumnName("PredictedLabel")]
        public uint PredictedClusterId { get; set; } // Cluster ID predicted by KMeans

        [ColumnName("Score")]
        public float[] Distances { get; set; } // Distances to each cluster centroid
    }
}
