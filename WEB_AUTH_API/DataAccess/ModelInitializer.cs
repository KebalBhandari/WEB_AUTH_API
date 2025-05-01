using Microsoft.Data.SqlClient;
using Microsoft.ML;
using System.Data;
using WEB_AUTH_API.Models;

namespace WEB_AUTH_API.DataAccess
{
    public class ModelInitializer
    {
        private readonly DataHandeler _dataHandler;
        private readonly string _modelPath = "behavior_model.zip";
        private readonly ILogger<ModelInitializer> _logger;

        public ModelInitializer(DataHandeler dataHandler, ILogger<ModelInitializer> logger)
        {
            _dataHandler = dataHandler;
            _logger = logger;
        }

        public void InitializeModel()
        {
            var context = new MLContext();

            if (File.Exists(_modelPath))
            {
                _logger.LogInformation("Trained model found. Skipping model training...");
                return;
            }

            if (IsDataAvailable())
            {
                _logger.LogInformation("Data found in the database. Training model using database data...");
                TrainModelUsingDatabase(context);
            }
            else
            {
                _logger.LogInformation("No data found in the database. Creating baseline model...");
                CreateBaselineModel(context);
            }
        }

        private bool IsDataAvailable()
        {
            try
            {
                string sqlQuery = "SELECT COUNT(*) AS DataCount FROM Timings";
                DataTable result = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);
                int dataCount = Convert.ToInt32(result.Rows[0]["DataCount"]);
                return dataCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking data availability: {ex.Message}");
                return false;
            }
        }

        private void TrainModelUsingDatabase(MLContext context)
        {
            try
            {
                var trainingData = new List<UserFeatures>();
                string sqlQuery = "SELECT DISTINCT UserId FROM Timings";
                DataTable userIdsTable = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);

                foreach (DataRow row in userIdsTable.Rows)
                {
                    int userId = Convert.ToInt32(row["UserId"]);
                    var userRawData = GetRawDataFromDb(userId);
                    trainingData.AddRange(userRawData);
                }

                if (!trainingData.Any())
                {
                    _logger.LogWarning("No training data found.");
                    return;
                }

                // Enhanced filtering of invalid data (include KeydownTime, KeyupTime)
                trainingData = trainingData.Where(f =>
                    !float.IsNaN(f.TimingInterval) && !float.IsInfinity(f.TimingInterval) &&
                    !float.IsNaN(f.KeyHoldDuration) && !float.IsInfinity(f.KeyHoldDuration) &&
                    !float.IsNaN(f.KeydownTime) && !float.IsInfinity(f.KeydownTime) &&
                    !float.IsNaN(f.KeyupTime) && !float.IsInfinity(f.KeyupTime) &&
                    !float.IsNaN(f.DotReactionTime) && !float.IsInfinity(f.DotReactionTime) &&
                    !float.IsNaN(f.ShapeReactionTime) && !float.IsInfinity(f.ShapeReactionTime) &&
                    !float.IsNaN(f.ShapeAccuracy) && !float.IsInfinity(f.ShapeAccuracy) &&
                    !float.IsNaN(f.MouseVelocity) && !float.IsInfinity(f.MouseVelocity) &&
                    !float.IsNaN(f.BackspacePress) && !float.IsInfinity(f.BackspacePress) &&
                    !float.IsNaN(f.BackspaceInterval) && !float.IsInfinity(f.BackspaceInterval)
                ).ToList();

                if (!trainingData.Any())
                {
                    _logger.LogWarning("No valid training data after filtering NaN/Infinity values.");
                    return;
                }

                _logger.LogInformation($"Training with {trainingData.Count} data points.");

                var dataView = context.Data.LoadFromEnumerable(trainingData);

                var pipeline = context.Transforms.Concatenate("Features",
                        nameof(UserFeatures.TimingInterval),
                        nameof(UserFeatures.KeyHoldDuration),
                        nameof(UserFeatures.KeydownTime),    // newly added
                        nameof(UserFeatures.KeyupTime),      // newly added
                        nameof(UserFeatures.DotReactionTime),
                        nameof(UserFeatures.ShapeReactionTime),
                        nameof(UserFeatures.ShapeAccuracy),
                        nameof(UserFeatures.MouseVelocity),
                        nameof(UserFeatures.BackspacePress),
                        nameof(UserFeatures.BackspaceInterval))
                    .Append(context.Transforms.ReplaceMissingValues("Features"))
                    .Append(context.Transforms.NormalizeMeanVariance("Features"))
                    .Append(context.AnomalyDetection.Trainers.RandomizedPca(
                        featureColumnName: "Features",
                        rank: 8 // Updated to reflect additional features
                    ));

                var model = pipeline.Fit(dataView);
                context.Model.Save(model, dataView.Schema, _modelPath);
                _logger.LogInformation("Model trained and saved successfully.");

                EvaluateModel(context, dataView, model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during model training: {ex.Message}");
                throw;
            }
        }


        private void EvaluateModel(MLContext context, IDataView testData, ITransformer model)
        {
            try
            {
                // Transform the test data to get predictions
                var predictions = model.Transform(testData);

                // Convert predictions to an enumerable for analysis
                var predictionResults = context.Data.CreateEnumerable<PcaAnomalyPrediction>(
                    predictions, reuseRowObject: false);

                // Analyze predictions
                int totalCount = 0;
                int anomalyCount = 0;
                double totalScore = 0;
                var scores = new List<float>();

                foreach (var prediction in predictionResults)
                {
                    totalCount++;
                    totalScore += prediction.Score;
                    scores.Add(prediction.Score);
                    if (prediction.IsAnomaly)
                    {
                        anomalyCount++;
                    }
                }

                if (totalCount == 0)
                {
                    _logger.LogWarning("No predictions generated for evaluation.");
                    return;
                }

                // Calculate basic statistics
                double averageScore = totalScore / totalCount;
                double anomalyRate = (double)anomalyCount / totalCount;
                double scoreStdDev = scores.Any() ? Math.Sqrt(scores.Average(s => Math.Pow(s - averageScore, 2))) : 0;

                _logger.LogInformation("Model evaluation results (unsupervised):");
                _logger.LogInformation($"  Total data points: {totalCount}");
                _logger.LogInformation($"  Anomalies detected: {anomalyCount}");
                _logger.LogInformation($"  Anomaly rate: {anomalyRate:P2}"); // Percentage format
                _logger.LogInformation($"  Average anomaly score: {averageScore:F4}");
                _logger.LogInformation($"  Score standard deviation: {scoreStdDev:F4}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during model evaluation: {ex.Message}");
            }
        }

        private void CreateBaselineModel(MLContext context)
        {
            try
            {
                var placeholderData = new List<UserFeatures>
        {
            new UserFeatures
            {
                UserId = 0,
                AttemptNumber = 1,
                DataId = 1,
                TimingInterval = 0.1f,
                KeyHoldDuration = 0.2f,
                KeydownTime = 1000f,
                KeyupTime = 1200f,
                DotReactionTime = 0.3f,
                ShapeReactionTime = 0.4f,
                ShapeAccuracy = 1.0f,
                MouseVelocity = 0.5f,
                BackspacePress = 1.0f,
                BackspaceInterval = 100.0f,
                DetectedLanguage = "en"
            },
            new UserFeatures
            {
                UserId = 0,
                AttemptNumber = 1,
                DataId = 2,
                TimingInterval = 0.12f,
                KeyHoldDuration = 0.22f,
                KeydownTime = 1100f,
                KeyupTime = 1320f,
                DotReactionTime = 0.32f,
                ShapeReactionTime = 0.42f,
                ShapeAccuracy = 0.0f,
                MouseVelocity = 0.6f,
                BackspacePress = 0.0f,
                BackspaceInterval = 0.0f,
                DetectedLanguage = "en"
            }
        };

                var dataView = context.Data.LoadFromEnumerable(placeholderData);

                var pipeline = context.Transforms.Concatenate("Features",
                        nameof(UserFeatures.TimingInterval),
                        nameof(UserFeatures.KeyHoldDuration),
                        nameof(UserFeatures.KeydownTime),
                        nameof(UserFeatures.KeyupTime),
                        nameof(UserFeatures.DotReactionTime),
                        nameof(UserFeatures.ShapeReactionTime),
                        nameof(UserFeatures.ShapeAccuracy),
                        nameof(UserFeatures.MouseVelocity),
                        nameof(UserFeatures.BackspacePress),
                        nameof(UserFeatures.BackspaceInterval))
                    .Append(context.Transforms.ReplaceMissingValues("Features"))
                    .Append(context.Transforms.NormalizeMeanVariance("Features"))
                    .Append(context.AnomalyDetection.Trainers.RandomizedPca(
                        featureColumnName: "Features",
                        rank: 5 // Adjusted to account for additional features
                    ));

                var model = pipeline.Fit(dataView);
                context.Model.Save(model, dataView.Schema, _modelPath);
                _logger.LogInformation("Baseline model created and saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating baseline model: {ex.Message}");
                throw;
            }
        }


        public PcaAnomalyPrediction PredictAnomaly(MLContext context, UserFeatures inputData)
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    throw new FileNotFoundException("Trained model not found. Please train the model first.");
                }

                ITransformer model = context.Model.Load(_modelPath, out var schema);
                var predictionEngine = context.Model.CreatePredictionEngine<UserFeatures, PcaAnomalyPrediction>(model);
                return predictionEngine.Predict(inputData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during prediction: {ex.Message}");
                throw;
            }
        }

        public UserBehaviorDataModel GetDataFromDb(int userId)
        {
            try
            {
                var parametersForTimings = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable timingData = _dataHandler.ReadData("GetTimings", parametersForTimings, CommandType.StoredProcedure);

                var timings = timingData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => row.Field<double>("IntervalValue")).ToList())
                    .ToList();

                var parametersForKeyHoldTimes = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable keyHoldData = _dataHandler.ReadData("GetKeyHoldTimes", parametersForKeyHoldTimes, CommandType.StoredProcedure);

                var keyHoldTimes = keyHoldData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => new KeyHoldTime
                    {
                        KeydownTime = row.Field<double>("KeydownTime"),
                        KeyupTime = row.Field<double>("KeyupTime"),
                        Duration = row.Field<double>("Duration")
                    }).ToList())
                    .ToList();

                var parametersForDotTimings = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable dotTimingData = _dataHandler.ReadData("GetDotTimings", parametersForDotTimings, CommandType.StoredProcedure);

                var dotTimings = dotTimingData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => row.Field<double>("ReactionTime")).ToList())
                    .ToList();

                var parametersForShapeTimings = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable shapeTimingData = _dataHandler.ReadData("GetShapeTimings", parametersForShapeTimings, CommandType.StoredProcedure);

                var shapeTimings = shapeTimingData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => new ShapeTiming
                    {
                        ReactionTime = row.Field<double>("ReactionTime"),
                        IsCorrect = row.Field<bool>("IsCorrect") ? 1 : 0
                    }).ToList())
                    .ToList();

                var parametersForMouseMovements = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable mouseMovementData = _dataHandler.ReadData("GetMouseMovements", parametersForMouseMovements, CommandType.StoredProcedure);

                var mouseMovements = mouseMovementData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => new MouseMovement
                    {
                        Time = row.Field<double>("Time"),
                        X = row.Field<double>("X"),
                        Y = row.Field<double>("Y"),
                        Velocity = row.Field<double>("Velocity"),
                        Slope = row.Field<double>("Slope")
                    }).ToList())
                    .ToList();

                var parametersForBackspaceTimings = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable backspaceTimingData = _dataHandler.ReadData("GetBackspaceTimings", parametersForBackspaceTimings, CommandType.StoredProcedure);

                var backspaceTimings = backspaceTimingData.AsEnumerable()
                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                    .Select(g => g.Select(row => new BackSpaceTiming
                    {
                        Time = row.Field<double>("Time"),
                        Action = row.Field<string>("Action")
                    }).ToList())
                    .ToList();

                var parametersForDetectedLanguages = new SqlParameter[] { new SqlParameter("@UserId", userId) };
                DataTable detectedLanguageData = _dataHandler.ReadData("GetDetectedLanguages", parametersForDetectedLanguages, CommandType.StoredProcedure);

                var detectedLanguages = detectedLanguageData.AsEnumerable()
                    .Select(row => row.Field<string>("DetectedLanguage"))
                    .ToList();

                return new UserBehaviorDataModel
                {
                    UserId = userId,
                    Timings = timings,
                    KeyHoldTimes = keyHoldTimes,
                    DotTimings = dotTimings,
                    ShapeTimings = shapeTimings,
                    ShapeMouseMovements = mouseMovements,
                    BackSpaceTimings = backspaceTimings,
                    DetectedLanguages = detectedLanguages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching data from database: {ex.Message}");
                throw;
            }
        }

        private List<UserFeatures> ExtractFeaturesPerAttempt(UserBehaviorDataModel data)
        {
            var allFeatures = new List<UserFeatures>();
            var random = new Random();

            var counts = new List<int>();

            if (data.Timings?.Any() == true)
                counts.Add(data.Timings.Max(t => t?.Count ?? 0));
            if (data.KeyHoldTimes?.Any() == true)
                counts.Add(data.KeyHoldTimes.Max(k => k?.Count ?? 0));
            if (data.DotTimings?.Any() == true)
                counts.Add(data.DotTimings.Max(d => d?.Count ?? 0));
            if (data.ShapeTimings?.Any() == true)
                counts.Add(data.ShapeTimings.Max(s => s?.Count ?? 0));
            if (data.ShapeMouseMovements?.Any() == true)
                counts.Add(data.ShapeMouseMovements.Max(m => m?.Count ?? 0));
            if (data.BackSpaceTimings?.Any() == true)
                counts.Add(data.BackSpaceTimings.Max(b => b?.Count ?? 0));

            int maxDataPoints = counts.Any() ? counts.Max() : 0;



            int numberOfAttempts = new int[]
            {
                data.Timings?.Count ?? 0,
                data.KeyHoldTimes?.Count ?? 0,
                data.DotTimings?.Count ?? 0,
                data.ShapeTimings?.Count ?? 0,
                data.ShapeMouseMovements?.Count ?? 0,
                data.BackSpaceTimings?.Count ?? 0,
                data.DetectedLanguages?.Count ?? 0
            }.Min();

            if (numberOfAttempts == 0 || maxDataPoints == 0)
            {
                _logger.LogWarning("No valid attempts or data points found for user {UserId}", data.UserId);
                return allFeatures;
            }

            for (int attemptIndex = 0; attemptIndex < numberOfAttempts; attemptIndex++)
            {
                var timings = PadList(data.Timings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);

                var keyHoldTimes = PadList(
                    data.KeyHoldTimes?.ElementAtOrDefault(attemptIndex),
                    maxDataPoints,
                    new KeyHoldTime
                    {
                        KeydownTime = 0.0,
                        KeyupTime = 0.0,
                        Duration = 0.0
                    },
                    random
                );

                var dotTimings = PadList(data.DotTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);

                var shapeTimings = PadList(
                    data.ShapeTimings?.ElementAtOrDefault(attemptIndex),
                    maxDataPoints,
                    new ShapeTiming { ReactionTime = 0.0, IsCorrect = 0 },
                    random
                );

                var mouseMovements = PadList(
                    data.ShapeMouseMovements?.ElementAtOrDefault(attemptIndex),
                    maxDataPoints,
                    new MouseMovement { X = 0.0, Y = 0.0, Time = 0.0, Velocity = 0.0, Slope = 0.0 },
                    random
                );

                var backSpaceTimings = PadList(
                    data.BackSpaceTimings?.ElementAtOrDefault(attemptIndex),
                    maxDataPoints,
                    new BackSpaceTiming { Time = 0.0, Action = "" },
                    random
                );

                for (int dataIndex = 0; dataIndex < maxDataPoints; dataIndex++)
                {
                    var features = new UserFeatures
                    {
                        UserId = data.UserId,
                        AttemptNumber = attemptIndex + 1,
                        DataId = dataIndex + 1,
                        TimingInterval = (float)timings[dataIndex],
                        KeyHoldDuration = (float)keyHoldTimes[dataIndex].Duration,
                        KeydownTime = (float)keyHoldTimes[dataIndex].KeydownTime,
                        KeyupTime = (float)keyHoldTimes[dataIndex].KeyupTime,
                        DotReactionTime = (float)dotTimings[dataIndex],
                        ShapeReactionTime = (float)shapeTimings[dataIndex].ReactionTime,
                        ShapeAccuracy = (float)shapeTimings[dataIndex].IsCorrect,
                        MouseVelocity = (float)mouseMovements[dataIndex].Velocity,
                        BackspaceInterval = (float)backSpaceTimings[dataIndex].Time,
                        BackspacePress = !string.IsNullOrEmpty(backSpaceTimings[dataIndex].Action) ? 1.0f : 0.0f,
                        DetectedLanguage = attemptIndex < data.DetectedLanguages?.Count
                                           ? data.DetectedLanguages[attemptIndex]
                                           : "eng"
                    };

                    allFeatures.Add(features);
                }
            }

            _logger.LogInformation($"Extracted {allFeatures.Count} feature rows for UserId {data.UserId}");
            return allFeatures;
        }


        private List<T> PadList<T>(IEnumerable<T> source, int targetLength, T defaultValue, Random random)
        {
            if (source == null || !source.Any())
                return Enumerable.Repeat(defaultValue, targetLength).ToList();

            var sourceList = source.ToList();
            int sourceCount = sourceList.Count;

            if (sourceCount >= targetLength)
                return sourceList.Take(targetLength).ToList();

            var paddedList = new List<T>(sourceList);
            double mean;

            if (typeof(T) == typeof(double))
            {
                mean = sourceList.Cast<double>().Average();
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    paddedList.Add((T)(object)randomValue);
                }
            }
            else if (typeof(T) == typeof(KeyHoldTime))
            {
                var keyHoldList = sourceList.Cast<KeyHoldTime>().ToList();

                double meanDuration = keyHoldList.Any() ? keyHoldList.Average(k => k.Duration) : 100.0;
                double meanKeydown = keyHoldList.Any() ? keyHoldList.Average(k => k.KeydownTime) : 1000.0;

                while (paddedList.Count < targetLength)
                {
                    double variationDuration = meanDuration * 0.1;
                    double variationKeydown = meanKeydown * 0.1;

                    double randomDuration = meanDuration + (random.NextDouble() * 2 - 1) * variationDuration;
                    double randomKeydown = meanKeydown + (random.NextDouble() * 2 - 1) * variationKeydown;

                    paddedList.Add((T)(object)new KeyHoldTime
                    {
                        KeydownTime = Math.Max(0, randomKeydown),
                        Duration = Math.Max(0, randomDuration),
                        KeyupTime = Math.Max(0, randomKeydown + randomDuration)
                    });
                }
            }

            else if (typeof(T) == typeof(ShapeTiming))
            {
                mean = sourceList.Cast<ShapeTiming>().Average(s => s.ReactionTime);
                double accuracyMean = sourceList.Cast<ShapeTiming>().Average(s => s.IsCorrect);
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    int randomAccuracy = random.NextDouble() < accuracyMean ? 1 : 0;
                    paddedList.Add((T)(object)new ShapeTiming { ReactionTime = randomValue, IsCorrect = randomAccuracy });
                }
            }
            else if (typeof(T) == typeof(MouseMovement))
            {
                mean = sourceList.Cast<MouseMovement>().Average(m => m.Velocity);
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    paddedList.Add((T)(object)new MouseMovement { Velocity = randomValue });
                }
            }
            else if (typeof(T) == typeof(BackSpaceTiming))
            {
                mean = sourceList.Cast<BackSpaceTiming>().Average(b => b.Time);
                double actionProbability = sourceList.Cast<BackSpaceTiming>().Average(b => b.Action != null ? 1.0 : 0.0);
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    string randomAction = random.NextDouble() < actionProbability ? "Backspace" : null;
                    paddedList.Add((T)(object)new BackSpaceTiming { Time = randomValue, Action = randomAction });
                }
            }
            else
            {
                paddedList.AddRange(Enumerable.Repeat(defaultValue, targetLength - sourceCount));
            }

            return paddedList.Take(targetLength).ToList();
        }

        private List<UserFeatures> GetRawDataFromDb(int userId)
        {
            try
            {
                var userData = GetDataFromDb(userId);
                return ExtractFeaturesPerAttempt(userData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting raw data from database: {ex.Message}");
                throw;
            }
        }

        internal UserFeatures ExtractFeatures(UserBehaviorDataModel userData)
        {
            throw new NotImplementedException();
        }
    }
}