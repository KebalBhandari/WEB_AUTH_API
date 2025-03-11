using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WEB_AUTH_API.Models;
using Microsoft.ML;
using WEB_AUTH_API.DataAccess;

namespace WEB_AUTH_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrainModelController : ControllerBase
    {
        private readonly DataHandeler _dataHandler;
        private readonly ModelInitializer _modelInitializer;
        private readonly MLContext _mlContext;
        private readonly ILogger<TrainModelController> _logger;

        public TrainModelController(
            DataHandeler dataHandler,
            ModelInitializer modelInitializer,
            MLContext mlContext,
            ILogger<TrainModelController> logger)
        {
            _dataHandler = dataHandler;
            _modelInitializer = modelInitializer;
            _mlContext = mlContext;
            _logger = logger;
        }

        [HttpPost("SaveData")]
        public async Task<IActionResult> SaveData([FromBody] UserDataModel userDataModel)
        {
            try
            {
                if (userDataModel == null || string.IsNullOrEmpty(userDataModel.TokenNo))
                {
                    _logger.LogWarning("Invalid user data provided: TokenNo={TokenNo}", userDataModel?.TokenNo);
                    return BadRequest(new
                    {
                        Status = "ERROR",
                        Message = "Invalid user data provided. TokenNo is required.",
                        Timestamp = DateTime.UtcNow
                    });
                }

                var tasks = new List<Task>();

                tasks.Add(InsertTimingsAsync(userDataModel));
                tasks.Add(InsertKeyHoldTimesAsync(userDataModel));
                tasks.Add(InsertDotTimingsAsync(userDataModel));
                tasks.Add(InsertShapeTimingsAsync(userDataModel));
                tasks.Add(InsertMouseMovementsAsync(userDataModel));
                tasks.Add(InsertBackspaceTimingsAsync(userDataModel));
                tasks.Add(InsertDetectedLanguagesAsync(userDataModel));

                // Wait for all insert tasks to complete
                await Task.WhenAll(tasks);

                _logger.LogInformation("Data saved successfully for TokenNo={TokenNo}", userDataModel.TokenNo);
                return Ok(new { Status = "SUCCESS", Message = "Data Saved Successfully", Timestamp = DateTime.UtcNow });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error while saving data for TokenNo={TokenNo}", userDataModel?.TokenNo);
                return StatusCode(500, new
                {
                    Status = "ERROR",
                    Message = "A database error occurred while saving data.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while saving data for TokenNo={TokenNo}", userDataModel?.TokenNo);
                return StatusCode(500, new
                {
                    Status = "ERROR",
                    Message = "An unexpected error occurred while saving data.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task InsertTimingsAsync(UserDataModel userDataModel)
        {
            foreach (var (timingList, attemptNumber) in userDataModel.Timings.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var interval in timingList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@IntervalValue", interval)
                    };

                    int result = await _dataHandler.InsertAsync("InsertTimings", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert timing data.");
                }
            }
        }

        private async Task InsertKeyHoldTimesAsync(UserDataModel userDataModel)
        {
            foreach (var (keyHoldList, attemptNumber) in userDataModel.KeyHoldTimes.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var keyHold in keyHoldList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@Duration", keyHold.Duration)
                    };

                    int result = await _dataHandler.InsertAsync("InsertKeyHoldTimes", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert key hold time data.");
                }
            }
        }

        private async Task InsertDotTimingsAsync(UserDataModel userDataModel)
        {
            foreach (var (dotTimingList, attemptNumber) in userDataModel.DotTimings.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var reactionTime in dotTimingList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@ReactionTime", reactionTime)
                    };

                    int result = await _dataHandler.InsertAsync("InsertDotTimings", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert dot timing data.");
                }
            }
        }

        private async Task InsertShapeTimingsAsync(UserDataModel userDataModel)
        {
            foreach (var (shapeTimingList, attemptNumber) in userDataModel.ShapeTimings.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var shapeTiming in shapeTimingList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@ReactionTime", shapeTiming.ReactionTime),
                        new SqlParameter("@IsCorrect", shapeTiming.IsCorrect)
                    };

                    int result = await _dataHandler.InsertAsync("InsertShapeTimings", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert shape timing data.");
                }
            }
        }

        private async Task InsertMouseMovementsAsync(UserDataModel userDataModel)
        {
            foreach (var (mouseMovementList, attemptNumber) in userDataModel.ShapeMouseMovements.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var mouseMovement in mouseMovementList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@Time", mouseMovement.Time),
                        new SqlParameter("@X", mouseMovement.X),
                        new SqlParameter("@Y", mouseMovement.Y),
                        new SqlParameter("@velocity", mouseMovement.Velocity),
                        new SqlParameter("@slope", mouseMovement.Slope)
                    };

                    int result = await _dataHandler.InsertAsync("InsertMouseMovements", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert mouse movement data.");
                }
            }
        }

        private async Task InsertBackspaceTimingsAsync(UserDataModel userDataModel)
        {
            foreach (var (backspaceTimingList, attemptNumber) in userDataModel.BackspaceTimings.Select((value, idx) => (value, idx + 1)))
            {
                foreach (var backspaceTiming in backspaceTimingList)
                {
                    var parameters = new SqlParameter[]
                    {
                        new SqlParameter("@UserId", userDataModel.TokenNo),
                        new SqlParameter("@AttemptNumber", attemptNumber),
                        new SqlParameter("@Time", backspaceTiming.Time),
                        new SqlParameter("@Action", backspaceTiming.Action)
                    };

                    int result = await _dataHandler.InsertAsync("InsertBackspaceTimings", parameters, CommandType.StoredProcedure);
                    if (result <= 0)
                        throw new Exception("Failed to insert backspace timing data.");
                }
            }
        }

        private async Task InsertDetectedLanguagesAsync(UserDataModel userDataModel)
        {
            foreach (var (detectedLanguage, attemptNumber) in userDataModel.DetectedLanguages.Select((value, idx) => (value, idx + 1)))
            {
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@UserId", userDataModel.TokenNo),
                    new SqlParameter("@AttemptNumber", attemptNumber),
                    new SqlParameter("@DetectedLanguage", detectedLanguage)
                };

                int result = await _dataHandler.InsertAsync("InsertDetectedLanguages", parameters, CommandType.StoredProcedure);
                if (result <= 0)
                    throw new Exception("Failed to insert detected language data.");
            }
        }

        [HttpPost("Predict")]
        public IActionResult Predict([FromBody] UserBehaviorDataModel userData)
        {
            try
            {
                // Validate input
                if (userData == null)
                {
                    _logger.LogWarning("Invalid user data provided: UserId={UserId}", userData?.UserId);
                    return BadRequest(new
                    {
                        Status = "ERROR",
                        Message = "Invalid user data provided. UserId must be greater than 0.",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Log the start of prediction
                _logger.LogInformation("Starting prediction for UserId={UserId}", userData.UserId);

                // Extract features from the provided userData (new input)
                List<UserFeatures> inputFeatures = ExtractFeaturesPerAttempt(userData);

                // Load the model and create prediction engine
                string modelPath = "behavior_model.zip";
                ITransformer model = _mlContext.Model.Load(modelPath, out var schema);
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<UserFeatures, PcaAnomalyPrediction>(model, inputSchema: schema);

                // Predict using the latest input features (assuming the last attempt is the most recent)
                UserFeatures latestFeatures = inputFeatures.LastOrDefault() ?? throw new InvalidOperationException("No valid features extracted from input data.");
                var prediction = predictionEngine.Predict(latestFeatures);

                // Calculate results
                bool isAnomaly = prediction.IsAnomaly;
                float score = prediction.Score;
                const float MAX_POSSIBLE_SCORE = 10f; // Adjust based on your model's typical score range
                float rawConfidence = 1f - (Math.Abs(score) / MAX_POSSIBLE_SCORE); // Use Abs to handle negative scores
                float confidence = Math.Clamp(rawConfidence, 0f, 1f) * 100f; // Convert to percentage

                // Log the prediction result
                _logger.LogInformation("Prediction completed for UserId={UserId}: IsAnomaly={IsAnomaly}, Score={Score}, Confidence={Confidence}%",
                    userData.UserId, isAnomaly, score, confidence);

                // Build response
                var response = new
                {
                    Status = "SUCCESS",
                    IsAnomaly = isAnomaly,
                    AnomalyScore = score,
                    ConfidencePercentage = confidence,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Model file 'behavior_model.zip' not found for UserId={UserId}. Ensure the model is trained and available.", userData?.UserId);
                return StatusCode(503, new
                {
                    Status = "ERROR",
                    Message = "Model file not found. Please ensure the model is trained and saved as 'behavior_model.zip'.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation during prediction for UserId={UserId}.", userData?.UserId);
                return StatusCode(500, new
                {
                    Status = "ERROR",
                    Message = "An error occurred during prediction. Invalid operation.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during prediction for UserId={UserId}.", userData?.UserId);
                return StatusCode(500, new
                {
                    Status = "ERROR",
                    Message = "An unexpected error occurred during prediction.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // Helper method to extract features per attempt (copied and adapted from ModelInitializer)
        private List<UserFeatures> ExtractFeaturesPerAttempt(UserBehaviorDataModel data)
        {
            var allFeatures = new List<UserFeatures>();
            var random = new Random(); // For generating random values near the mean

            int maxDataPoints = new int[]
            {
        data.Timings?.Max(t => t?.Count ?? 0) ?? 0,
        data.KeyHoldTimes?.Max(k => k?.Count ?? 0) ?? 0,
        data.DotTimings?.Max(d => d?.Count ?? 0) ?? 0,
        data.ShapeTimings?.Max(s => s?.Count ?? 0) ?? 0,
        data.ShapeMouseMovements?.Max(m => m?.Count ?? 0) ?? 0,
        data.BackspaceTimings?.Max(b => b?.Count ?? 0) ?? 0
            }.Max();

            int numberOfAttempts = new int[]
            {
        data.Timings?.Count ?? 0,
        data.KeyHoldTimes?.Count ?? 0,
        data.DotTimings?.Count ?? 0,
        data.ShapeTimings?.Count ?? 0,
        data.ShapeMouseMovements?.Count ?? 0,
        data.BackspaceTimings?.Count ?? 0,
        data.DetectedLanguages?.Count ?? 0
            }.Min();

            if (numberOfAttempts == 0 || maxDataPoints == 0)
            {
                _logger.LogWarning("No valid attempts or data points found for user {UserId}", data.UserId);
                return allFeatures;
            }

            for (int attemptIndex = 0; attemptIndex < numberOfAttempts; attemptIndex++)
            {
                // Pad each feature list to match maxDataPoints with random values near the mean
                var timings = PadList(data.Timings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);
                var keyHoldTimes = PadList(data.KeyHoldTimes?.ElementAtOrDefault(attemptIndex), maxDataPoints, new KeyHoldTime { Duration = 0.0 }, random);
                var dotTimings = PadList(data.DotTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);
                var shapeTimings = PadList(data.ShapeTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, new ShapeTiming { ReactionTime = 0.0, IsCorrect = 0 }, random);
                var mouseMovements = PadList(data.ShapeMouseMovements?.ElementAtOrDefault(attemptIndex), maxDataPoints, new MouseMovement { Velocity = 0.0 }, random);
                var backspaceTimings = PadList(data.BackspaceTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, new BackspaceTiming { Time = 0.0 }, random);

                // Create UserFeatures for each data point index
                for (int dataIndex = 0; dataIndex < maxDataPoints; dataIndex++)
                {
                    var features = new UserFeatures
                    {
                        UserId = data.UserId,
                        AttemptNumber = attemptIndex + 1,
                        DataId = dataIndex + 1,
                        TimingInterval = (float)timings[dataIndex],
                        KeyHoldDuration = (float)keyHoldTimes[dataIndex].Duration,
                        DotReactionTime = (float)dotTimings[dataIndex],
                        ShapeReactionTime = (float)shapeTimings[dataIndex].ReactionTime,
                        ShapeAccuracy = (float)shapeTimings[dataIndex].IsCorrect,
                        MouseVelocity = (float)mouseMovements[dataIndex].Velocity,
                        BackspaceInterval = (float)backspaceTimings[dataIndex].Time,
                        BackspacePress = backspaceTimings[dataIndex].Action != null ? 1.0f : 0.0f,
                        DetectedLanguage = attemptIndex < data.DetectedLanguages?.Count ? data.DetectedLanguages[attemptIndex] : "unknown"
                    };

                    allFeatures.Add(features);
                }
            }

            _logger.LogInformation($"Extracted {allFeatures.Count} feature rows for UserId {data.UserId}");
            return allFeatures;
        }

        // Helper method to pad a list with random values near the mean (copied and adapted from ModelInitializer)
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

            // Calculate mean based on type T
            if (typeof(T) == typeof(double))
            {
                mean = sourceList.Cast<double>().Average();
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1; // ±10% of mean
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation; // Random value within ±variation
                    paddedList.Add((T)(object)randomValue);
                }
            }
            else if (typeof(T) == typeof(KeyHoldTime))
            {
                mean = sourceList.Cast<KeyHoldTime>().Average(k => k.Duration);
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    paddedList.Add((T)(object)new KeyHoldTime { Duration = randomValue });
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
                    int randomAccuracy = random.NextDouble() < accuracyMean ? 1 : 0; // Randomly assign based on mean accuracy
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
            else if (typeof(T) == typeof(BackspaceTiming))
            {
                mean = sourceList.Cast<BackspaceTiming>().Average(b => b.Time);
                double actionProbability = sourceList.Cast<BackspaceTiming>().Average(b => b.Action != null ? 1.0 : 0.0);
                while (paddedList.Count < targetLength)
                {
                    double variation = mean * 0.1;
                    double randomValue = mean + (random.NextDouble() * 2 - 1) * variation;
                    string randomAction = random.NextDouble() < actionProbability ? "Backspace" : null;
                    paddedList.Add((T)(object)new BackspaceTiming { Time = randomValue, Action = randomAction });
                }
            }
            else
            {
                // Fallback to default value if type is not handled
                paddedList.AddRange(Enumerable.Repeat(defaultValue, targetLength - sourceCount));
            }

            return paddedList.Take(targetLength).ToList();
        }
    }
}