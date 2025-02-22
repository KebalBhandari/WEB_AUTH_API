
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WEB_AUTH_API.DataAccess;
using WEB_AUTH_API.Models;
using Microsoft.ML;

namespace WEB_AUTH_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrainModelController : ControllerBase
    {

        private readonly DataHandeler _dataHandler;
        private readonly IConfiguration _configuration;

        public TrainModelController(IConfiguration configuration)
        {
            _dataHandler = new DataHandeler();
            _configuration = configuration;
        }

        [HttpPost("SaveData")]
        public async Task<IActionResult> SaveData([FromBody] UserDataModel userDataModel)
        {
            try
            {
                var tasks = new List<Task>();

                // 1) Insert Timings in a separate task
                tasks.Add(Task.Run(() =>
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

                            int result = _dataHandler.Insert("InsertTimings", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert timing data.");
                        }
                    }
                }));

                // 2) Insert KeyHoldTimes in another task
                tasks.Add(Task.Run(() =>
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

                            int result = _dataHandler.Insert("InsertKeyHoldTimes", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert key hold time data.");
                        }
                    }
                }));

                // 3) Insert DotTimings
                tasks.Add(Task.Run(() =>
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

                            int result = _dataHandler.Insert("InsertDotTimings", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert dot timing data.");
                        }
                    }
                }));

                // 4) Insert ShapeTimings
                tasks.Add(Task.Run(() =>
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

                            int result = _dataHandler.Insert("InsertShapeTimings", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert shape timing data.");
                        }
                    }
                }));

                // 5) Insert MouseMovements
                tasks.Add(Task.Run(() =>
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
                        new SqlParameter("@Y", mouseMovement.Y)
                            };

                            int result = _dataHandler.Insert("InsertMouseMovements", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert mouse movement data.");
                        }
                    }
                }));

                // 6) Insert BackspaceTimings
                tasks.Add(Task.Run(() =>
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

                            int result = _dataHandler.Insert("InsertBackspaceTimings", parameters, CommandType.StoredProcedure);
                            if (result <= 0)
                                throw new Exception("Failed to insert backspace timing data.");
                        }
                    }
                }));

                // Wait for all insert tasks to complete
                await Task.WhenAll(tasks);

                return StatusCode(200, new { Status = "SUCCESS", Message = "Data Saved Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost("Predict")]
        public IActionResult Predict([FromBody] UserBehaviorDataModel userData)
        {
            try
            {
                UserFeatures extractedFeatures = ExtractFeatures(userData);

                var context = new MLContext();
                ITransformer model = context.Model.Load("behavior_model.zip", out var schema);

                var predictionEngine = context.Model.CreatePredictionEngine<UserFeatures, PcaAnomalyPrediction>(model, inputSchema: schema);

                var prediction = predictionEngine.Predict(extractedFeatures);

                bool isAnomaly = prediction.IsAnomaly;
                float score = prediction.Score;    

                float maxPossibleScore = 10f;   
                float rawConfidence = 1f - (score / maxPossibleScore);
                float confidence = Math.Clamp(rawConfidence, 0f, 1f);

                return Ok(new
                {
                    Status = "SUCCESS",
                    IsAnomaly = isAnomaly,
                    Score = score,
                    Confidence = confidence 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }

        public UserFeatures ExtractFeatures(UserBehaviorDataModel data)
        {
            var features = new UserFeatures();

            // 1. Timing Features
            var allTimings = data.Timings.SelectMany(t => t).ToList();
            features.AvgTimingInterval = (float)allTimings.Average();
            features.StdDevTimingInterval = (float)Math.Sqrt(allTimings.Average(v => Math.Pow(v - features.AvgTimingInterval, 2)));

            // 2. Key Hold Time Features
            var allKeyHoldDurations = data.KeyHoldTimes.SelectMany(k => k).Select(k => k.Duration).ToList();
            features.AvgKeyHoldDuration = (float)allKeyHoldDurations.Average();
            features.StdDevKeyHoldDuration = (float)Math.Sqrt(allKeyHoldDurations.Average(v => Math.Pow(v - features.AvgKeyHoldDuration, 2)));

            // 3. Dot Timing Features
            var allDotTimings = data.DotTimings.SelectMany(d => d).ToList();
            features.AvgDotReactionTime = (float)allDotTimings.Average();

            // 5. Mouse Movement Features
            var allMouseMovements = data.ShapeMouseMovements.SelectMany(m => m).ToList();
            if (allMouseMovements.Count > 1)
            {
                double totalDistance = 0;
                double totalTime = 0;

                for (int i = 1; i < allMouseMovements.Count; i++)
                {
                    var dx = allMouseMovements[i].X - allMouseMovements[i - 1].X;
                    var dy = allMouseMovements[i].Y - allMouseMovements[i - 1].Y;
                    var dt = allMouseMovements[i].Time - allMouseMovements[i - 1].Time;

                    totalDistance += Math.Sqrt(dx * dx + dy * dy);
                    totalTime += dt;
                }

                features.AvgMouseSpeed = (float)(totalDistance / totalTime);
            }
            else
            {
                features.AvgMouseSpeed = 0;
            }

            return features;
        }
    }
}
