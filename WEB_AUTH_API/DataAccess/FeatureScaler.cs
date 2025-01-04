using WEB_AUTH_API.Models;

namespace WEB_AUTH_API.DataAccess
{
    public class FeatureScaler
    {
        private double[] minValues;
        private double[] maxValues;

        // Fit method: Calculate min and max for each feature
        public void Fit(List<double[]> features)
        {
            int numFeatures = features[0].Length;
            minValues = new double[numFeatures];
            maxValues = new double[numFeatures];

            // Initialize min and max with extreme values
            for (int i = 0; i < numFeatures; i++)
            {
                minValues[i] = double.MaxValue;
                maxValues[i] = double.MinValue;
            }

            foreach (var featureSet in features)
            {
                for (int i = 0; i < numFeatures; i++)
                {
                    if (featureSet[i] < minValues[i]) minValues[i] = featureSet[i];
                    if (featureSet[i] > maxValues[i]) maxValues[i] = featureSet[i];
                }
            }
        }

        // Transform method: Apply min-max scaling
        public List<double[]> Transform(List<double[]> features)
        {
            return features.Select(f => ScaleFeature(f)).ToList();
        }

        // Scale a single feature array
        public double[] ScaleFeature(double[] featureSet)
        {
            double[] scaledFeatures = new double[featureSet.Length];
            for (int i = 0; i < featureSet.Length; i++)
            {
                scaledFeatures[i] = (featureSet[i] - minValues[i]) / (maxValues[i] - minValues[i]);
            }
            return scaledFeatures;
        }
    }

}
