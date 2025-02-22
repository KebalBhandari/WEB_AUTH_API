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

            if (File.Exists(_modelPath))
            {
                Console.WriteLine("Trained model found. Skipping model training...");
                return;
            }

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
            string sqlQuery = "SELECT COUNT(*) AS DataCount FROM Timings";
            DataTable result = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);
            int dataCount = Convert.ToInt32(result.Rows[0]["DataCount"]);

            return dataCount > 0;
        }

        private void TrainModelUsingDatabase(MLContext context)
        {
            var trainingData = new List<UserFeatures>();

            // Fetch all unique user IDs from the database
            string sqlQuery = "SELECT DISTINCT UserId FROM Timings";
            DataTable userIdsTable = _dataHandler.ReadData(sqlQuery, null, CommandType.Text);

            // Iterate through each user and extract features
            foreach (DataRow row in userIdsTable.Rows)
            {
                int userId = Convert.ToInt32(row["UserId"]);
                UserBehaviorDataModel userData = GetDataFromDb(userId);
                var userFeaturesForAllAttempts = ExtractFeaturesPerAttempt(userData);

                trainingData.AddRange(userFeaturesForAllAttempts);
            }

            // Load training data into ML.NET data view
            var dataView = context.Data.LoadFromEnumerable(trainingData);

            // Define the ML pipeline
            var pipeline = context.Transforms.Concatenate("Features",
                                                          nameof(UserFeatures.AvgTimingInterval),
                                                          nameof(UserFeatures.StdDevTimingInterval),
                                                          nameof(UserFeatures.AvgKeyHoldDuration),
                                                          nameof(UserFeatures.StdDevKeyHoldDuration),
                                                          nameof(UserFeatures.AvgDotReactionTime),
                                                          nameof(UserFeatures.AvgShapeReactionTime),
                                                          nameof(UserFeatures.ShapeAccuracy),
                                                          nameof(UserFeatures.AvgMouseSpeed),
                                                          nameof(UserFeatures.BackspacePressCount), // New feature
                                                          nameof(UserFeatures.AvgBackspaceInterval)) // New feature
                            .Append(context.Transforms.Conversion.MapValueToKey("Features"))
                            .Append(context.Transforms.Conversion.MapKeyToValue("Features"))
                            .Append(context.Transforms.ReplaceMissingValues("Features"))
                            .Append(context.Transforms.NormalizeMeanVariance("Features", "Features"))
                            .Append(context.AnomalyDetection.Trainers.RandomizedPca(
                                featureColumnName: "Features",
                                rank: 4
                            ));

            // Train the model
            var model = pipeline.Fit(dataView);

            // Save the trained model
            context.Model.Save(model, dataView.Schema, _modelPath);

            Console.WriteLine("Model trained and saved successfully.");
        }


        private void CreateBaselineModel(MLContext context)
        {
            // Create placeholder data for the baseline model
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
            AvgMouseSpeed = 0.5f,
            BackspacePressCount = 2, // New feature
            AvgBackspaceInterval = 100.0f // New feature
        }
    };

            // Load placeholder data into ML.NET data view
            var dataView = context.Data.LoadFromEnumerable(placeholderData);

            // Define the ML pipeline
            var pipeline = context.Transforms.Concatenate("Features",
                                                          nameof(UserFeatures.AvgTimingInterval),
                                                          nameof(UserFeatures.StdDevTimingInterval),
                                                          nameof(UserFeatures.AvgKeyHoldDuration),
                                                          nameof(UserFeatures.StdDevKeyHoldDuration),
                                                          nameof(UserFeatures.AvgDotReactionTime),
                                                          nameof(UserFeatures.AvgShapeReactionTime),
                                                          nameof(UserFeatures.ShapeAccuracy),
                                                          nameof(UserFeatures.AvgMouseSpeed),
                                                          nameof(UserFeatures.BackspacePressCount), // New feature
                                                          nameof(UserFeatures.AvgBackspaceInterval)) // New feature
                           .Append(context.AnomalyDetection.Trainers.RandomizedPca("Features", rank: 4));

            // Train the baseline model
            var model = pipeline.Fit(dataView);

            // Save the baseline model
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

            var parametersForDotTimings = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId)
            };
            DataTable dotTimingData = _dataHandler.ReadData("GetDotTimings", parametersForDotTimings, CommandType.StoredProcedure);

            var dotTimings = dotTimingData.AsEnumerable()
                                          .GroupBy(row => row.Field<int>("AttemptNumber"))
                                          .Select(g => g.Select(row => row.Field<double>("ReactionTime")).ToList())
                                          .ToList();

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

            var parametersForBackspaceTimings = new SqlParameter[]
            {
        new SqlParameter("@UserId", userId)
            };
            DataTable backspaceTimingData = _dataHandler.ReadData("GetBackspaceTimings", parametersForBackspaceTimings, CommandType.StoredProcedure);

            var backspaceTimings = backspaceTimingData.AsEnumerable()
                                                      .GroupBy(row => row.Field<int>("AttemptNumber"))
                                                      .Select(g => g.Select(row => new BackspaceTiming
                                                      {
                                                          Time = row.Field<double>("Time"),
                                                          Action = row.Field<string>("Action")
                                                      }).ToList())
                                                      .ToList();

            return new UserBehaviorDataModel
            {
                UserId = userId,
                Timings = timings,
                KeyHoldTimes = keyHoldTimes,
                DotTimings = dotTimings,
                ShapeTimings = shapeTimings,
                ShapeMouseMovements = mouseMovements,
                BackspaceTimings = backspaceTimings
            };
        }

        private List<UserFeatures> ExtractFeaturesPerAttempt(UserBehaviorDataModel data)
        {
            var allFeatures = new List<UserFeatures>();
            int numberOfAttempts = new int[]
            {
                data.Timings.Count,
                data.KeyHoldTimes.Count,
                data.DotTimings.Count,
                data.ShapeTimings.Count,
                data.ShapeMouseMovements.Count,
                data.BackspaceTimings.Count
            }.Min();

            for (int attemptIndex = 0; attemptIndex < numberOfAttempts; attemptIndex++)
            {
                var attemptFeatures = new UserFeatures();
                var attemptTimings = data.Timings[attemptIndex];  // List<double>
                attemptFeatures.AvgTimingInterval = (float)attemptTimings.Average();
                attemptFeatures.StdDevTimingInterval = (float)Math.Sqrt(
                    attemptTimings.Average(v => Math.Pow(v - attemptFeatures.AvgTimingInterval, 2))
                );

                var attemptKeyHolds = data.KeyHoldTimes[attemptIndex]; // List<KeyHoldTime>
                var durations = attemptKeyHolds.Select(k => k.Duration).ToList();
                attemptFeatures.AvgKeyHoldDuration = (float)durations.Average();
                attemptFeatures.StdDevKeyHoldDuration = (float)Math.Sqrt(
                    durations.Average(v => Math.Pow(v - attemptFeatures.AvgKeyHoldDuration, 2))
                );

                var attemptDotTimings = data.DotTimings[attemptIndex]; // List<double>
                attemptFeatures.AvgDotReactionTime = (float)attemptDotTimings.Average();

                var attemptShapeTimings = data.ShapeTimings[attemptIndex]; // List<ShapeTiming>
                attemptFeatures.AvgShapeReactionTime = (float)attemptShapeTimings.Average(s => s.ReactionTime);
                attemptFeatures.ShapeAccuracy = (float)attemptShapeTimings.Average(s => s.IsCorrect);

                var attemptMouseMovements = data.ShapeMouseMovements[attemptIndex]; // List<MouseMovement>
                if (attemptMouseMovements.Count > 1)
                {
                    double totalDistance = 0;
                    double totalTime = 0;

                    for (int i = 1; i < attemptMouseMovements.Count; i++)
                    {
                        var dx = attemptMouseMovements[i].X - attemptMouseMovements[i - 1].X;
                        var dy = attemptMouseMovements[i].Y - attemptMouseMovements[i - 1].Y;
                        var dt = attemptMouseMovements[i].Time - attemptMouseMovements[i - 1].Time;

                        // Make sure dt > 0 to avoid division by zero
                        if (dt > 0)
                        {
                            totalDistance += Math.Sqrt(dx * dx + dy * dy);
                            totalTime += dt;
                        }
                    }

                    if (totalTime > 0)
                    {
                        attemptFeatures.AvgMouseSpeed = (float)(totalDistance / totalTime);
                    }
                    else
                    {
                        attemptFeatures.AvgMouseSpeed = 0;
                    }
                }
                else
                {
                    attemptFeatures.AvgMouseSpeed = 0;
                }


                // 6) Extract BackspaceTiming Features
                var attemptBackspaceTimings = data.BackspaceTimings[attemptIndex]; // List<BackspaceTiming>
                if (attemptBackspaceTimings.Count > 0)
                {
                    // Calculate the number of backspace presses
                    attemptFeatures.BackspacePressCount = attemptBackspaceTimings.Count(b => b.Action == "pressed");

                    // Calculate the average time between backspace presses
                    var backspacePressTimes = attemptBackspaceTimings
                        .Where(b => b.Action == "pressed")
                        .Select(b => b.Time)
                        .ToList();

                    if (backspacePressTimes.Count > 1)
                    {
                        double totalInterval = 0;
                        for (int i = 1; i < backspacePressTimes.Count; i++)
                        {
                            totalInterval += backspacePressTimes[i] - backspacePressTimes[i - 1];
                        }
                        attemptFeatures.AvgBackspaceInterval = (float)(totalInterval / (backspacePressTimes.Count - 1));
                    }
                    else
                    {
                        attemptFeatures.AvgBackspaceInterval = 0;
                    }
                }
                else
                {
                    attemptFeatures.BackspacePressCount = 0;
                    attemptFeatures.AvgBackspaceInterval = 0;
                }

                allFeatures.Add(attemptFeatures);
            }

            return allFeatures;
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

                    // Make sure dt > 0 to avoid division by zero
                    if (dt > 0)
                    {
                        totalDistance += Math.Sqrt(dx * dx + dy * dy);
                        totalTime += dt;
                    }
                }

                if (totalTime > 0)
                {
                    features.AvgMouseSpeed = (float)(totalDistance / totalTime);
                }
                else
                {
                    features.AvgMouseSpeed = 0;
                }
            }
            else
            {
                features.AvgMouseSpeed = 0;
            }

            // 6. Backspace Timing Features
            var allBackspaceTimings = data.BackspaceTimings.SelectMany(b => b).ToList();
            if (allBackspaceTimings.Count > 0)
            {
                // Calculate the number of backspace presses
                features.BackspacePressCount = allBackspaceTimings.Count(b => b.Action == "pressed");

                // Calculate the average time between backspace presses
                var backspacePressTimes = allBackspaceTimings
                    .Where(b => b.Action == "pressed")
                    .Select(b => b.Time)
                    .ToList();

                if (backspacePressTimes.Count > 1)
                {
                    double totalInterval = 0;
                    for (int i = 1; i < backspacePressTimes.Count; i++)
                    {
                        totalInterval += backspacePressTimes[i] - backspacePressTimes[i - 1];
                    }
                    features.AvgBackspaceInterval = (float)(totalInterval / (backspacePressTimes.Count - 1));
                }
                else
                {
                    features.AvgBackspaceInterval = 0;
                }
            }
            else
            {
                features.BackspacePressCount = 0;
                features.AvgBackspaceInterval = 0;
            }

            return features;
        }
    }

}
