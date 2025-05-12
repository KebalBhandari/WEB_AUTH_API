using WEB_AUTH_API.Models;

namespace WEB_AUTH_API.DataAccess
{
    public class FeatureScaler
    {
        private double[] minValues;
        private double[] maxValues;

        // =============================================================
        // Function: Fit
        // Computes min and max per feature column
        // =============================================================
        public void Fit(List<double[]> features)
        {
            int m = features[0].Length;
            minValues = Enumerable.Repeat(double.MaxValue, m).ToArray();
            maxValues = Enumerable.Repeat(double.MinValue, m).ToArray();

            foreach (var row in features)
            {
                for (int i = 0; i < m; i++)
                {
                    if (row[i] < minValues[i]) minValues[i] = row[i];
                    if (row[i] > maxValues[i]) maxValues[i] = row[i];
                }
            }
        }

        // =============================================================
        // Function: Transform
        // Applies min-max scaling to a list of feature arrays
        // =============================================================
        public List<double[]> Transform(List<double[]> features)
        {
            return features.Select(ScaleFeature).ToList();
        }

        // =============================================================
        // Function: ScaleFeature
        // Scales a single feature array
        // =============================================================
        public double[] ScaleFeature(double[] featureSet)
        {
            var scaled = new double[featureSet.Length];
            for (int i = 0; i < featureSet.Length; i++)
            {
                var range = maxValues[i] - minValues[i];
                scaled[i] = range > 0 ? (featureSet[i] - minValues[i]) / range : 0.0;
            }
            return scaled;
        }

    }
}
