using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Data;
using System.Data.Common;
using System.Transactions;
using WEB_AUTH_API.DataAccess;
using WEB_AUTH_API.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
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
        public IActionResult SaveData([FromBody] UserDataModel userDataModel)
        {
            try
            {
                foreach (var (timingList, attemptNumber) in userDataModel.Timings.Select((value, index) => (value, index + 1)))
                {
                    foreach (var interval in timingList)
                    {
                        var parameters_for_timings = new SqlParameter[]
                        {
                            new SqlParameter("@UserId", userDataModel.TokenNo),
                            new SqlParameter("@AttemptNumber", attemptNumber),
                            new SqlParameter("@IntervalValue", interval)
                        };
                        int result = _dataHandler.Insert("InsertTimings", parameters_for_timings, CommandType.StoredProcedure);
                        if (result <= 0) throw new Exception("Failed to insert timing data.");
                    }
                }

                // Insert Key Hold Times
                foreach (var (keyHoldList, attemptNumber) in userDataModel.KeyHoldTimes.Select((value, index) => (value, index + 1)))
                {
                    foreach (var keyHold in keyHoldList)
                    {
                        var parameters_for_keyHoldTimes = new SqlParameter[]
                        {
                                new SqlParameter("@UserId", userDataModel.TokenNo),
                                new SqlParameter("@AttemptNumber", attemptNumber),
                                new SqlParameter("@Duration", keyHold.Duration)
                        };
                        int result = _dataHandler.Insert("InsertKeyHoldTimes", parameters_for_keyHoldTimes, CommandType.StoredProcedure);
                        if (result <= 0) throw new Exception("Failed to insert key hold time data.");
                    }
                }

                // Insert Dot Timings
                foreach (var (dotTimingList, attemptNumber) in userDataModel.DotTimings.Select((value, index) => (value, index + 1)))
                {
                    foreach (var reactionTime in dotTimingList)
                    {
                        var parameters_for_dotTimings = new SqlParameter[]
                        {
                                new SqlParameter("@UserId", userDataModel.TokenNo),
                                new SqlParameter("@AttemptNumber", attemptNumber),
                                new SqlParameter("@ReactionTime", reactionTime)
                        };
                        int result = _dataHandler.Insert("InsertDotTimings", parameters_for_dotTimings, CommandType.StoredProcedure);
                        if (result <= 0) throw new Exception("Failed to insert dot timing data.");
                    }
                }

                // Insert Shape Timings
                foreach (var (shapeTimingList, attemptNumber) in userDataModel.ShapeTimings.Select((value, index) => (value, index + 1)))
                {
                    foreach (var shapeTiming in shapeTimingList)
                    {
                        var parameters_for_shapeTimings = new SqlParameter[]
                        {
                                new SqlParameter("@UserId", userDataModel.TokenNo),
                                new SqlParameter("@AttemptNumber", attemptNumber),
                                new SqlParameter("@ReactionTime", shapeTiming.ReactionTime),
                                new SqlParameter("@IsCorrect", shapeTiming.IsCorrect)
                        };
                        int result = _dataHandler.Insert("InsertShapeTimings", parameters_for_shapeTimings, CommandType.StoredProcedure);
                        if (result <= 0) throw new Exception("Failed to insert shape timing data.");
                    }
                }

                // Insert Mouse Movements
                foreach (var (mouseMovementList, attemptNumber) in userDataModel.ShapeMouseMovements.Select((value, index) => (value, index + 1)))
                {
                    foreach (var mouseMovement in mouseMovementList)
                    {
                        var parameters_for_mouseMovements = new SqlParameter[]
                        {
                                new SqlParameter("@UserId", userDataModel.TokenNo),
                                new SqlParameter("@AttemptNumber", attemptNumber),
                                new SqlParameter("@Time", mouseMovement.Time),
                                new SqlParameter("@X", mouseMovement.X),
                                new SqlParameter("@Y", mouseMovement.Y)
                        };
                        int result = _dataHandler.Insert("InsertMouseMovements", parameters_for_mouseMovements, CommandType.StoredProcedure);
                        if (result <= 0) throw new Exception("Failed to insert mouse movement data.");
                    }
                }
                return StatusCode(200, new { Status = "SUCCESS", Message ="Data Saved Successfully" });
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
                // Step 1: Extract features from the received data
                UserFeatures extractedFeatures = ExtractFeatures(userData);

                // Step 2: Load the trained model
                var context = new MLContext();
                ITransformer model = context.Model.Load("behavior_model.zip", out var schema);

                // Step 3: Create a prediction engine
                var predictionEngine = context.Model.CreatePredictionEngine<UserFeatures, ClusteringPrediction>(model);

                // Step 4: Predict the result
                var prediction = predictionEngine.Predict(extractedFeatures);

                // Step 5: Calculate the anomaly score based on cluster distances
                float minDistance = prediction.Distances.Min();
                float maxPossibleDistance = 10000f; // Adjust based on your dataset and observations
                float normalizedDistance = minDistance / maxPossibleDistance;
                float confidence = 1 - (minDistance / maxPossibleDistance); // Normalize to range [0, 1]
                confidence = Math.Max(0, Math.Min(confidence, 1));
                float threshold = 0.5f;

                bool isAnomaly = normalizedDistance > threshold;

                return Ok(new
                {
                    Status = "SUCCESS",
                    IsAnomaly = isAnomaly,
                    Confidence = confidence // The closer the distance, the higher the confidence
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

            // 4. Shape Timing Features
            var allShapeTimings = data.ShapeTimings.SelectMany(s => s).ToList();
            features.AvgShapeReactionTime = (float)allShapeTimings.Average(s => s.ReactionTime);
            features.ShapeAccuracy = (float)allShapeTimings.Average(s => s.IsCorrect);

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
