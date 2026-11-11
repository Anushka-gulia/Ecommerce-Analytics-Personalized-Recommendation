
using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        string inputPath =
            @"D:\CDA Activity 2\Dataset\otto-recsys-train.jsonl";

        string outputFolder = "Data";
        string resultsFolder = "Results";

        Directory.CreateDirectory(outputFolder);
        Directory.CreateDirectory(resultsFolder);

        string outputPath =
            Path.Combine(outputFolder, "processed_interactions.csv");

        Console.WriteLine("==================================");
        Console.WriteLine("OTTO DATA PREPROCESSING");
        Console.WriteLine("==================================");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Training dataset not found!");
            return;
        }

        int maxSessions = 100000;
        int sessionsProcessed = 0;
        long interactionsProcessed = 0;

        using (StreamReader reader = new StreamReader(inputPath))
        using (StreamWriter writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("SessionId,ProductId,Label");

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (sessionsProcessed >= maxSessions)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                using JsonDocument document =
                    JsonDocument.Parse(line);

                JsonElement root = document.RootElement;

                long sessionId =
                    root.GetProperty("session").GetInt64();

                JsonElement events =
                    root.GetProperty("events");

                foreach (JsonElement item in events.EnumerateArray())
                {
                    long productId =
                        item.GetProperty("aid").GetInt64();

                    string type =
                        item.GetProperty("type").GetString() ?? "";

                    float label = type switch
                    {
                        "clicks" => 1f,
                        "carts" => 3f,
                        "orders" => 5f,
                        _ => 0f
                    };

                    if (label > 0)
                    {
                        writer.WriteLine(
                            $"{sessionId},{productId},{label}");

                        interactionsProcessed++;
                    }
                }

                sessionsProcessed++;

                if (sessionsProcessed % 10000 == 0)
                    Console.WriteLine(
                        $"Sessions processed: {sessionsProcessed}");
            }
        }

        Console.WriteLine("\nPREPROCESSING COMPLETED");
        Console.WriteLine("Sessions: " + sessionsProcessed);
        Console.WriteLine("Interactions: " + interactionsProcessed);

        // ==================================
        // PRODUCT RECOMMENDATION SCORING
        // ==================================

        Console.WriteLine("\n==================================");
        Console.WriteLine("PRODUCT RECOMMENDATION MODEL");
        Console.WriteLine("==================================");

        Dictionary<long, double> productScores = new();
        Dictionary<long, int> productCounts = new();

        using (StreamReader reader = new StreamReader(outputPath))
        {
            string? line = reader.ReadLine(); // Skip header

            long records = 0;

            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(',');

                if (parts.Length != 3)
                    continue;

                if (!long.TryParse(parts[1], out long productId))
                    continue;

                if (!double.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double label))
                    continue;

                if (!productScores.ContainsKey(productId))
                {
                    productScores[productId] = 0;
                    productCounts[productId] = 0;
                }

                productScores[productId] += label;
                productCounts[productId]++;

                records++;

                if (records % 500000 == 0)
                    Console.WriteLine($"Scored records: {records}");
            }
        }

        string recommendationPath =
            Path.Combine(resultsFolder, "product_recommendations.csv");

        var topProducts = productScores
            .Select(p => new
            {
                ProductId = p.Key,
                Score = p.Value,
                Interactions = productCounts[p.Key]
            })
            .OrderByDescending(p => p.Score)
            .Take(20)
            .ToList();

        using (StreamWriter writer =
            new StreamWriter(recommendationPath))
        {
            writer.WriteLine("ProductId,RecommendationScore,Interactions");

            foreach (var product in topProducts)
            {
                writer.WriteLine(
                    $"{product.ProductId}," +
                    $"{product.Score.ToString(CultureInfo.InvariantCulture)}," +
                    $"{product.Interactions}");
            }
        }

        Console.WriteLine("\n==================================");
        Console.WriteLine("TOP 20 RECOMMENDED PRODUCTS");
        Console.WriteLine("==================================");

        foreach (var product in topProducts)
        {
            Console.WriteLine(
                $"Product: {product.ProductId} | " +
                $"Score: {product.Score} | " +
                $"Interactions: {product.Interactions}");
        }

        Console.WriteLine("\nRECOMMENDATION GENERATION COMPLETED");
        Console.WriteLine("Output: " + recommendationPath);
        // MACHINE LEARNING MODEL TRAINING

string modelPath = Path.Combine(
    "Models",
    "recommendation_model.zip"
);

RecommendationModel.TrainAndSave(
    outputPath,
    modelPath
);
string personalizedPath = Path.Combine(
    resultsFolder,
    "personalized_recommendations.csv"
);

RecommendationModel.GenerateRecommendations(
    outputPath,
    modelPath,
    recommendationPath,
    personalizedPath
);
// GENERATE DATA ANALYTICS REPORTS

Analytics.GenerateReports(
    outputPath,
    resultsFolder
);
    }
}
