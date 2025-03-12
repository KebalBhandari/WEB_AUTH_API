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
        public async Task<IActionResult> Predict([FromBody] UserDataModel userData)
        {
            try
            {
                // Validate input
                if (userData == null || string.IsNullOrEmpty(userData.TokenNo))
                {
                    _logger.LogWarning("Invalid user data provided: TokenNo={TokenNo}", userData?.TokenNo);
                    return BadRequest(new
                    {
                        Status = "ERROR",
                        Message = "Invalid user data provided. TokenNo is required.",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Get UserId from TokenNo using stored procedure
                int userId = Int32.Parse(await GetUserIdByTokenAsync(userData.TokenNo));

                if (userId == 0)
                {
                    _logger.LogWarning("No UserId found for TokenNo={TokenNo}", userData.TokenNo);
                    return BadRequest(new
                    {
                        Status = "ERROR",
                        Message = "No UserId found for the provided TokenNo.",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Map UserDataModel to UserBehaviorDataModel
                var userBehaviorData = MapToUserBehaviorDataModel(userData, userId);

                _logger.LogInformation("Starting prediction for UserId={UserId}", userId);

                List<UserFeatures> inputFeatures = ExtractFeaturesPerAttempt(userBehaviorData);
                string modelPath = "behavior_model.zip";
                ITransformer model = _mlContext.Model.Load(modelPath, out var schema);
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<UserFeatures, PcaAnomalyPrediction>(model, inputSchema: schema);
                UserFeatures latestFeatures = inputFeatures.LastOrDefault() ?? throw new InvalidOperationException("No valid features extracted from input data.");
                var prediction = predictionEngine.Predict(latestFeatures);

                bool isAnomaly = prediction.IsAnomaly;
                float score = prediction.Score;
                const float MAX_POSSIBLE_SCORE = 10f;
                float rawConfidence = 1f - (Math.Abs(score) / MAX_POSSIBLE_SCORE);
                float confidence = Math.Clamp(rawConfidence, 0f, 1f) * 100f;

                _logger.LogInformation("Prediction completed for UserId={UserId}: IsAnomaly={IsAnomaly}, Score={Score}, Confidence={Confidence}%",
                    userId, isAnomaly, score, confidence);

                await SavePredictionResultAsync(userData.TokenNo, isAnomaly, score, confidence, "v1.0"); 

                var response = new
                {
                    Status = "SUCCESS",
                    IsAnomaly = isAnomaly, 
                    AnomalyScore = score,
                    ConfidencePercentage = confidence,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Model file 'behavior_model.zip' not found for TokenNo={TokenNo}. Ensure the model is trained and available.", userData?.TokenNo);
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
                _logger.LogError(ex, "Invalid operation during prediction for TokenNo={TokenNo}.", userData?.TokenNo);
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
                _logger.LogError(ex, "An unexpected error occurred during prediction for TokenNo={TokenNo}.", userData?.TokenNo);
                return StatusCode(500, new
                {
                    Status = "ERROR",
                    Message = "An unexpected error occurred during prediction.",
                    Details = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task<string> GetUserIdByTokenAsync(string tokenNo)
        {
            var parameters = new SqlParameter[]
            {
        new SqlParameter("@TokenNo", tokenNo)
            };

            try
            {
                string result = await _dataHandler.ExecuteScalarAsync("getUserIdByToken", parameters, CommandType.StoredProcedure);
                if (string.IsNullOrEmpty(result))
                {
                    _logger.LogWarning("No UserId returned for TokenNo={TokenNo}. Database returned null or empty.", tokenNo);
                    throw new InvalidOperationException("No UserId found for the provided TokenNo.");
                }

                if (result.Length > 10 || !result.All(char.IsLetterOrDigit))
                {
                    _logger.LogWarning("Invalid UserId format returned for TokenNo={TokenNo}. Result={Result}", tokenNo, result);
                    throw new InvalidOperationException("Invalid UserId format returned from database.");
                }

                _logger.LogInformation("Successfully retrieved UserId={UserId} for TokenNo={TokenNo}", result, tokenNo);
                return result;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error while retrieving UserId for TokenNo={TokenNo}", tokenNo);
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving UserId for TokenNo={TokenNo}", tokenNo);
                throw; 
            }
        }

        private UserBehaviorDataModel MapToUserBehaviorDataModel(UserDataModel userData, int userId)
        {
            return new UserBehaviorDataModel
            {
                UserId = userId, 
                Timings = userData.Timings ?? new List<List<double>>(),
                KeyHoldTimes = userData.KeyHoldTimes ?? new List<List<KeyHoldTime>>(),
                DotTimings = userData.DotTimings ?? new List<List<double>>(),
                ShapeTimings = userData.ShapeTimings ?? new List<List<ShapeTiming>>(),
                ShapeMouseMovements = userData.ShapeMouseMovements ?? new List<List<MouseMovement>>(),
                BackspaceTimings = userData.BackspaceTimings ?? new List<List<BackspaceTiming>>(),
                DetectedLanguages = userData.DetectedLanguages ?? new List<string>()
            };
        }

        private async Task SavePredictionResultAsync(string userId, bool IsAnamoly, float score, float confidenceScore, string modelVersion)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId),
                new SqlParameter("@IsAnomaly", IsAnamoly),
                new SqlParameter("@Score", score),
                new SqlParameter("@ConfidenceScore", confidenceScore),
                new SqlParameter("@ModelVersion", modelVersion)
            };

            int result = await _dataHandler.InsertAsync("InsertPredictionResult", parameters, CommandType.StoredProcedure);
            if (result <= 0)
            {
                _logger.LogWarning("Failed to save prediction result for UserId={UserId}", userId);
                throw new Exception("Failed to insert prediction result into database.");
            }

            _logger.LogInformation("Prediction result saved successfully for UserId={UserId}", userId);
        }

        private List<UserFeatures> ExtractFeaturesPerAttempt(UserBehaviorDataModel data)
        {
            var allFeatures = new List<UserFeatures>();
            var random = new Random();

            if (data == null)
            {
                _logger.LogWarning("Input data is null");
                return allFeatures;
            }

            int maxDataPoints = new[]
    {
        data.Timings?.Any() == true ? data.Timings.Max(t => t?.Count ?? 0) : 0,
        data.KeyHoldTimes?.Any() == true ? data.KeyHoldTimes.Max(k => k?.Count ?? 0) : 0,
        data.DotTimings?.Any() == true ? data.DotTimings.Max(d => d?.Count ?? 0) : 0,
        data.ShapeTimings?.Any() == true ? data.ShapeTimings.Max(s => s?.Count ?? 0) : 0,
        data.ShapeMouseMovements?.Any() == true ? data.ShapeMouseMovements.Max(m => m?.Count ?? 0) : 0,
        data.BackspaceTimings?.Any() == true ? data.BackspaceTimings.Max(b => b?.Count ?? 0) : 0
    }.Max();

            // Calculate number of attempts with null checking
            int numberOfAttempts = new int[]
            {
        data.Timings?.Count ?? 0,
        data.KeyHoldTimes?.Count ?? 0,
        data.DotTimings?.Count ?? 0,
        data.ShapeTimings?.Count ?? 0,
        data.ShapeMouseMovements?.Count ?? 0,
        data.BackspaceTimings?.Count ?? 0,
        data.DetectedLanguages?.Count ?? 0
            }.Where(x => x > 0).DefaultIfEmpty(0).Min();

            if (numberOfAttempts == 0 || maxDataPoints == 0)
            {
                _logger.LogWarning("No valid attempts or data points found for user {UserId}", data.UserId);
                return allFeatures;
            }

            for (int attemptIndex = 0; attemptIndex < numberOfAttempts; attemptIndex++)
            {
                var timings = PadList(data.Timings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);
                var keyHoldTimes = PadList(data.KeyHoldTimes?.ElementAtOrDefault(attemptIndex), maxDataPoints, new KeyHoldTime { Duration = 0.0 }, random);
                var dotTimings = PadList(data.DotTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, 0.0, random);
                var shapeTimings = PadList(data.ShapeTimings?.ElementAtOrDefault(attemptIndex), maxDataPoints, new ShapeTiming { ReactionTime = 0.0, IsCorrect = 0 }, random);
                var mouseMovements = PadList(data.ShapeMouseMovements?.ElementAtOrDefault(attemptIndex), maxDataPoints, new MouseMovement { Velocity = 0.0 }, random);

                // Provide default empty backspace timings if null
                var backspaceTimings = PadList(
                    data.BackspaceTimings?.ElementAtOrDefault(attemptIndex) ?? new List<BackspaceTiming>(),
                    maxDataPoints,
                    new BackspaceTiming { Time = 0.0, Action = null },
                    random
                );

                for (int dataIndex = 0; dataIndex < maxDataPoints; dataIndex++)
                {
                    // Safely access backspace timing with fallback
                    var backspaceTiming = backspaceTimings[dataIndex] ?? new BackspaceTiming { Time = 0.0, Action = null };

                    var features = new UserFeatures
                    {
                        UserId = data.UserId,
                        AttemptNumber = attemptIndex + 1,
                        DataId = dataIndex + 1,
                        TimingInterval = (float)(timings?[dataIndex] ?? 0.0),
                        KeyHoldDuration = (float)(keyHoldTimes?[dataIndex]?.Duration ?? 0.0),
                        DotReactionTime = (float)(dotTimings?[dataIndex] ?? 0.0),
                        ShapeReactionTime = (float)(shapeTimings?[dataIndex]?.ReactionTime ?? 0.0),
                        ShapeAccuracy = (float)(shapeTimings?[dataIndex]?.IsCorrect ?? 0),
                        MouseVelocity = (float)(mouseMovements?[dataIndex]?.Velocity ?? 0.0),
                        BackspaceInterval = (float)(backspaceTiming.Time),
                        BackspacePress = backspaceTiming.Action != null ? 1.0f : 0.0f,
                        DetectedLanguage = attemptIndex < (data.DetectedLanguages?.Count ?? 0)
                            ? data.DetectedLanguages[attemptIndex]
                            : "unknown"
                    };

                    allFeatures.Add(features);
                }
            }

            _logger.LogInformation($"Extracted {allFeatures.Count} feature rows for UserId {data.UserId}");
            return allFeatures;
        }

        private List<T> PadList<T>(IEnumerable<T> source, int targetLength, T defaultValue, Random random)
        {
            // Handle null or empty input
            if (source == null || !source.Any())
                return Enumerable.Repeat(defaultValue, targetLength).ToList();

            var sourceList = source.ToList();
            int sourceCount = sourceList.Count;

            // Return truncated or original list if no padding needed
            if (sourceCount >= targetLength)
                return sourceList.Take(targetLength).ToList();

            var paddedList = new List<T>(sourceList);

            try
            {
                if (typeof(T) == typeof(double))
                {
                    var values = sourceList.Cast<double>();
                    double mean = values.Any() ? values.Average() : 0.0;
                    double stdDev = values.Any() ? Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2))) : 0.1;

                    while (paddedList.Count < targetLength)
                    {
                        double randomValue = mean + (random.NextDouble() * 2 - 1) * stdDev * 0.5; // Using 0.5 * stdDev as variation
                        paddedList.Add((T)(object)Math.Max(0, randomValue)); // Ensure non-negative
                    }
                }
                else if (typeof(T) == typeof(KeyHoldTime))
                {
                    var values = sourceList.Cast<KeyHoldTime>();
                    double mean = values.Any() ? values.Average(k => k.Duration) : 0.0;
                    double stdDev = values.Any() ? Math.Sqrt(values.Average(k => Math.Pow(k.Duration - mean, 2))) : 0.1;

                    while (paddedList.Count < targetLength)
                    {
                        double randomValue = mean + (random.NextDouble() * 2 - 1) * stdDev * 0.5;
                        paddedList.Add((T)(object)new KeyHoldTime { Duration = Math.Max(0, randomValue) });
                    }
                }
                else if (typeof(T) == typeof(ShapeTiming))
                {
                    var values = sourceList.Cast<ShapeTiming>();
                    double mean = values.Any() ? values.Average(s => s.ReactionTime) : 0.0;
                    double stdDev = values.Any() ? Math.Sqrt(values.Average(s => Math.Pow(s.ReactionTime - mean, 2))) : 0.1;
                    double accuracyMean = values.Any() ? values.Average(s => s.IsCorrect) : 0.5;

                    while (paddedList.Count < targetLength)
                    {
                        double randomValue = mean + (random.NextDouble() * 2 - 1) * stdDev * 0.5;
                        int randomAccuracy = random.NextDouble() < accuracyMean ? 1 : 0;
                        paddedList.Add((T)(object)new ShapeTiming
                        {
                            ReactionTime = Math.Max(0, randomValue),
                            IsCorrect = randomAccuracy
                        });
                    }
                }
                else if (typeof(T) == typeof(MouseMovement))
                {
                    var values = sourceList.Cast<MouseMovement>();
                    double mean = values.Any() ? values.Average(m => m.Velocity) : 0.0;
                    double stdDev = values.Any() ? Math.Sqrt(values.Average(m => Math.Pow(m.Velocity - mean, 2))) : 0.1;

                    while (paddedList.Count < targetLength)
                    {
                        double randomValue = mean + (random.NextDouble() * 2 - 1) * stdDev * 0.5;
                        paddedList.Add((T)(object)new MouseMovement
                        {
                            Velocity = Math.Max(0, randomValue)
                        });
                    }
                }
                else if (typeof(T) == typeof(BackspaceTiming))
                {
                    var values = sourceList.Cast<BackspaceTiming>();
                    double mean = values.Any() ? values.Average(b => b.Time) : 0.0;
                    double stdDev = values.Any() ? Math.Sqrt(values.Average(b => Math.Pow(b.Time - mean, 2))) : 0.1;
                    double actionProbability = values.Any() ? values.Average(b => b.Action != null ? 1.0 : 0.0) : 0.5;

                    while (paddedList.Count < targetLength)
                    {
                        double randomValue = mean + (random.NextDouble() * 2 - 1) * stdDev * 0.5;
                        string randomAction = random.NextDouble() < actionProbability ? "Backspace" : null;
                        paddedList.Add((T)(object)new BackspaceTiming
                        {
                            Time = Math.Max(0, randomValue),
                            Action = randomAction
                        });
                    }
                }
                else
                {
                    paddedList.AddRange(Enumerable.Repeat(defaultValue, targetLength - sourceCount));
                }
            }
            catch (Exception ex)
            {
                // Log the error and fall back to default padding
                Console.WriteLine($"Error in PadList: {ex.Message}");
                paddedList = new List<T>(sourceList);
                paddedList.AddRange(Enumerable.Repeat(defaultValue, targetLength - sourceCount));
            }

            return paddedList.Take(targetLength).ToList();
        }
    }
}