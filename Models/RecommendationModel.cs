
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

public static class RecommendationModel
{
    // ==========================================
    // DATA CLASSES
    // ==========================================

    public class InteractionData
    {
        [LoadColumn(0)]
        public long SessionId { get; set; }

        [LoadColumn(1)]
        public long ProductId { get; set; }

        [LoadColumn(2)]
        public float Label { get; set; }
    }

    public class RecommendationPrediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }

    // ==========================================
    // TRAIN AND SAVE MACHINE LEARNING MODEL
    // ==========================================

    public static void TrainAndSave(
        string inputPath,
        string modelPath)
    {
        Console.WriteLine("\n==================================");
        Console.WriteLine("MACHINE LEARNING MODEL TRAINING");
        Console.WriteLine("==================================");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Training dataset not found!");
            return;
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(modelPath))!);

        MLContext mlContext = new MLContext(seed: 42);

        Console.WriteLine("Loading training dataset...");

        IDataView trainingData =
            mlContext.Data.LoadFromTextFile<InteractionData>(
                inputPath,
                hasHeader: true,
                separatorChar: ',');

        // Encode session IDs and product IDs
        var dataProcessPipeline =
            mlContext.Transforms.Conversion.MapValueToKey(
                outputColumnName: "SessionIdEncoded",
                inputColumnName: nameof(InteractionData.SessionId))

            .Append(
                mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "ProductIdEncoded",
                    inputColumnName: nameof(InteractionData.ProductId)));

        // Matrix Factorization algorithm
        var options = new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = "SessionIdEncoded",
            MatrixRowIndexColumnName = "ProductIdEncoded",
            LabelColumnName = nameof(InteractionData.Label),

            NumberOfIterations = 20,
            ApproximationRank = 16,
            LearningRate = 0.01f,
            Lambda = 0.025f
        };

        var trainingPipeline =
            dataProcessPipeline.Append(
                mlContext.Recommendation()
                    .Trainers.MatrixFactorization(options));

        Console.WriteLine("Training Matrix Factorization model...");

        ITransformer trainedModel =
            trainingPipeline.Fit(trainingData);

        // Save trained model
        mlContext.Model.Save(
            trainedModel,
            trainingData.Schema,
            modelPath);

        Console.WriteLine("\n==================================");
        Console.WriteLine("MODEL TRAINING COMPLETED");
        Console.WriteLine("==================================");

        Console.WriteLine(
            "Algorithm: Matrix Factorization");

        Console.WriteLine(
            "Model saved at: " + modelPath);
    }

    // ==========================================
    // GENERATE PERSONALIZED RECOMMENDATIONS
    // ==========================================

    public static void GenerateRecommendations(
        string inputPath,
        string modelPath,
        string popularProductsPath,
        string outputPath)
    {
        Console.WriteLine("\n==================================");
        Console.WriteLine("PERSONALIZED RECOMMENDATIONS");
        Console.WriteLine("==================================");

        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Trained model not found!");
            return;
        }

        if (!File.Exists(inputPath) ||
            !File.Exists(popularProductsPath))
        {
            Console.WriteLine("Required input files are missing!");
            return;
        }

        MLContext mlContext = new MLContext(seed: 42);

        // Load trained model
        Console.WriteLine("Loading trained model...");

        ITransformer model =
            mlContext.Model.Load(modelPath, out var modelSchema);

        var predictionEngine =
            mlContext.Model.CreatePredictionEngine<
                InteractionData,
                RecommendationPrediction>(model);

        // ======================================
        // FIND LATEST SESSION
        // ======================================

        long sessionId = -1;

        HashSet<long> interactedProducts = new();

        using (StreamReader reader =
            new StreamReader(inputPath))
        {
            reader.ReadLine(); // Skip CSV header

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(',');

                if (parts.Length < 3)
                    continue;

                if (!long.TryParse(
                    parts[0],
                    out long currentSession))
                    continue;

                if (!long.TryParse(
                    parts[1],
                    out long productId))
                    continue;

                if (currentSession != sessionId)
                {
                    sessionId = currentSession;
                    interactedProducts.Clear();
                }

                interactedProducts.Add(productId);
            }
        }

        if (sessionId == -1)
        {
            Console.WriteLine("No valid session found!");
            return;
        }

        Console.WriteLine(
            "Selected Session ID: " + sessionId);

        // ======================================
        // LOAD POPULAR PRODUCTS
        // ======================================

        List<long> candidateProducts = new();

        using (StreamReader reader =
            new StreamReader(popularProductsPath))
        {
            reader.ReadLine(); // Skip header

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(',');

                if (parts.Length == 0)
                    continue;

                if (long.TryParse(
                    parts[0],
                    out long productId))
                {
                    candidateProducts.Add(productId);
                }
            }
        }

        // Remove products already interacted with
        candidateProducts = candidateProducts
            .Distinct()
            .Where(p => !interactedProducts.Contains(p))
            .ToList();

        Console.WriteLine(
            "Candidate Products: " + candidateProducts.Count);

        // ======================================
        // PREDICT PRODUCT SCORES
        // ======================================

        var predictions = new List<(
            long ProductId,
            float Score)>();

        foreach (long productId in candidateProducts)
        {
            InteractionData input = new InteractionData
            {
                SessionId = sessionId,
                ProductId = productId,
                Label = 0f
            };

            RecommendationPrediction prediction =
                predictionEngine.Predict(input);

            predictions.Add((
                productId,
                prediction.Score));
        }

        // Rank products by predicted score
        var topRecommendations = predictions
            .OrderByDescending(p => p.Score)
            .Take(10)
            .ToList();

        // ======================================
        // SAVE RECOMMENDATIONS
        // ======================================

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                Path.GetFullPath(outputPath))!);

        using (StreamWriter writer =
            new StreamWriter(outputPath))
        {
            writer.WriteLine(
                "SessionId,ProductId,PredictedScore");

            foreach (var item in topRecommendations)
            {
                writer.WriteLine(
                    $"{sessionId}," +
                    $"{item.ProductId}," +
                    item.Score.ToString(
                        "F6",
                        CultureInfo.InvariantCulture));
            }
        }

        // ======================================
        // DISPLAY TOP RECOMMENDATIONS
        // ======================================

        Console.WriteLine("\n==================================");
        Console.WriteLine("TOP 10 PERSONALIZED PRODUCTS");
        Console.WriteLine("==================================");

        foreach (var item in topRecommendations)
        {
            Console.WriteLine(
                $"Product: {item.ProductId} | " +
                $"Predicted Score: {item.Score:F4}");
        }

        Console.WriteLine("\n==================================");
        Console.WriteLine("RECOMMENDATION GENERATION COMPLETED");
        Console.WriteLine("==================================");

        Console.WriteLine(
            "Output saved at: " + outputPath);

        predictionEngine.Dispose();
    }
}
