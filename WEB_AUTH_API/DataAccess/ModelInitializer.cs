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

        public ModelInitializer(DataHandeler dataHandler)
        {
            _dataHandler = dataHandler;
        }

        public void InitializeModel()
        {
            var context = new MLContext();

            // Step 1: Check if the model already exists
            if (File.Exists(_modelPath))
            {
                Console.WriteLine("Trained model found. Skipping model training...");
                return;
            }

            // Step 2: Check if data is present in the database
            if (IsDataAvailable())
            {
                Console.WriteLine("Data found in the database. Training model using database data...");
                TrainModelUsingDatabase(context);
            }
            else
            {
                Console.WriteLine("No data found in the database. Creating baseline model...");
                CreateBaselineModel(context);
            }
        }

        private bool IsDataAvailable()
        {
            // Query to check if any data is present in the database
            string sqlQuery = "SELECT COUNT(*) AS DataCount FROM Timings";
            DataTable result = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);
            int dataCount = Convert.ToInt32(result.Rows[0]["DataCount"]);

            return dataCount > 0;
        }

        private void TrainModelUsingDatabase(MLContext context)
        {
            var trainingData = new List<UserFeatures>();

            // Fetch user IDs for training
            string sqlQuery = "SELECT DISTINCT UserId FROM Timings";
            DataTable userIdsTable = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);

            foreach (DataRow row in userIdsTable.Rows)
            {
                int userId = Convert.ToInt32(row["UserId"]);
                UserBehaviorDataModel userData = GetDataFromDb(userId);
                UserFeatures features = ExtractFeatures(userData);
                trainingData.Add(features);
            }

            // Train the model
            var dataView = context.Data.LoadFromEnumerable(trainingData);
            var pipeline = context.Transforms.Concatenate("Features",
                                                          nameof(UserFeatures.AvgTimingInterval),
                                                          nameof(UserFeatures.StdDevTimingInterval),
                                                          nameof(UserFeatures.AvgKeyHoldDuration),
                                                          nameof(UserFeatures.StdDevKeyHoldDuration),
                                                          nameof(UserFeatures.AvgDotReactionTime),
                                                          nameof(UserFeatures.AvgShapeReactionTime),
                                                          nameof(UserFeatures.ShapeAccuracy),
                                                          nameof(UserFeatures.AvgMouseSpeed))
                            .Append(context.Transforms.Conversion.MapValueToKey("Features"))
                            .Append(context.Transforms.Conversion.MapKeyToValue("Features"))
                            .Append(context.Transforms.ReplaceMissingValues("Features"))
                            .Append(context.Clustering.Trainers.KMeans("Features", numberOfClusters: 1));


            var model = pipeline.Fit(dataView);

            // Save the trained model
            context.Model.Save(model, dataView.Schema, _modelPath);

            Console.WriteLine("Model trained and saved successfully.");
        }


        private void CreateBaselineModel(MLContext context)
        {
            // Placeholder data
            var placeholderData = new List<UserFeatures>
        {
            new UserFeatures
            {
                AvgTimingInterval = 0.1f,
                StdDevTimingInterval = 0.05f,
                AvgKeyHoldDuration = 0.2f,
                StdDevKeyHoldDuration = 0.1f,
                AvgDotReactionTime = 0.3f,
                AvgShapeReactionTime = 0.4f,
                ShapeAccuracy = 0.9f,
                AvgMouseSpeed = 0.5f
            }
        };

            var dataView = context.Data.LoadFromEnumerable(placeholderData);
            var pipeline = context.Transforms.Concatenate("Features",
                                                          nameof(UserFeatures.AvgTimingInterval),
                                                          nameof(UserFeatures.StdDevTimingInterval),
                                                          nameof(UserFeatures.AvgKeyHoldDuration),
                                                          nameof(UserFeatures.StdDevKeyHoldDuration),
                                                          nameof(UserFeatures.AvgDotReactionTime),
                                                          nameof(UserFeatures.AvgShapeReactionTime),
                                                          nameof(UserFeatures.ShapeAccuracy),
                                                          nameof(UserFeatures.AvgMouseSpeed))
                           .Append(context.AnomalyDetection.Trainers.RandomizedPca("Features", rank: 4));

            var model = pipeline.Fit(dataView);

            context.Model.Save(model, dataView.Schema, _modelPath);

            Console.WriteLine("Baseline model created and saved successfully.");
        }

        public UserBehaviorDataModel GetDataFromDb(int userId)
        {
            var parametersForTimings = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable timingData = _dataHandler.ReadData("GetTimings", parametersForTimings, CommandType.StoredProcedure);

            var timings = timingData.AsEnumerable()
                                    .GroupBy(row => row.Field<int>("AttemptNumber"))
                                    .Select(g => g.Select(row => row.Field<double>("IntervalValue")).ToList())
                                    .ToList();

            // 2. Fetch Key Hold Times
            var parametersForKeyHoldTimes = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable keyHoldData = _dataHandler.ReadData("GetKeyHoldTimes", parametersForKeyHoldTimes, CommandType.StoredProcedure);

            var keyHoldTimes = keyHoldData.AsEnumerable()
                                          .GroupBy(row => row.Field<int>("AttemptNumber"))
                                          .Select(g => g.Select(row => new KeyHoldTime
                                          {
                                              Duration = row.Field<double>("Duration")
                                          }).ToList())
                                          .ToList();

            // 3. Fetch Dot Timings
            var parametersForDotTimings = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable dotTimingData = _dataHandler.ReadData("GetDotTimings", parametersForDotTimings, CommandType.StoredProcedure);

            var dotTimings = dotTimingData.AsEnumerable()
                                          .GroupBy(row => row.Field<int>("AttemptNumber"))
                                          .Select(g => g.Select(row => row.Field<double>("ReactionTime")).ToList())
                                          .ToList();

            // 4. Fetch Shape Timings
            var parametersForShapeTimings = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable shapeTimingData = _dataHandler.ReadData("GetShapeTimings", parametersForShapeTimings, CommandType.StoredProcedure);

            var shapeTimings = shapeTimingData.AsEnumerable()
                                              .GroupBy(row => row.Field<int>("AttemptNumber"))
                                              .Select(g => g.Select(row => new ShapeTiming
                                              {
                                                  ReactionTime = row.Field<double>("ReactionTime"),
                                                  IsCorrect = row.Field<bool>("IsCorrect") ? 1 : 0
                                              }).ToList())
                                              .ToList();

            // 5. Fetch Mouse Movements
            var parametersForMouseMovements = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable mouseMovementData = _dataHandler.ReadData("GetMouseMovements", parametersForMouseMovements, CommandType.StoredProcedure);

            var mouseMovements = mouseMovementData.AsEnumerable()
                                                  .GroupBy(row => row.Field<int>("AttemptNumber"))
                                                  .Select(g => g.Select(row => new MouseMovement
                                                  {
                                                      Time = row.Field<double>("Time"),
                                                      X = row.Field<double>("X"),
                                                      Y = row.Field<double>("Y")
                                                  }).ToList())
                                                  .ToList();

            // Construct UserBehaviorDataModel
            return new UserBehaviorDataModel
            {
                UserId = userId,
                Timings = timings,
                KeyHoldTimes = keyHoldTimes,
                DotTimings = dotTimings,
                ShapeTimings = shapeTimings,
                ShapeMouseMovements = mouseMovements
            };
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
