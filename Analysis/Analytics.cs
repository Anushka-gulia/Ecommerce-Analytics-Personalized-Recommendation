
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

public static class Analytics
{
    public static void GenerateReports(
        string inputPath,
        string resultsFolder)
    {
        Console.WriteLine("\n==================================");
        Console.WriteLine("E-COMMERCE DATA ANALYSIS");
        Console.WriteLine("==================================");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Processed dataset not found!");
            return;
        }

        Directory.CreateDirectory(resultsFolder);

        int clicks = 0;
        int carts = 0;
        int orders = 0;

        long totalRecords = 0;

        Dictionary<long, int> productCounts = new();
        Dictionary<long, int> sessionCounts = new();

        using (StreamReader reader = new StreamReader(inputPath))
        {
            reader.ReadLine();

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(',');

                if (parts.Length != 3)
                    continue;

                if (!long.TryParse(parts[0], out long sessionId))
                    continue;

                if (!long.TryParse(parts[1], out long productId))
                    continue;

                if (!float.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float label))
                    continue;

                totalRecords++;

                if (label == 1f)
                    clicks++;
                else if (label == 3f)
                    carts++;
                else if (label == 5f)
                    orders++;

                if (!productCounts.ContainsKey(productId))
                    productCounts[productId] = 0;

                productCounts[productId]++;

                if (!sessionCounts.ContainsKey(sessionId))
                    sessionCounts[sessionId] = 0;

                sessionCounts[sessionId]++;
            }
        }

        // ======================================
        // 1. INTERACTION DISTRIBUTION
        // ======================================

        string interactionPath = Path.Combine(
            resultsFolder,
            "interaction_distribution.csv");

        using (StreamWriter writer =
            new StreamWriter(interactionPath))
        {
            writer.WriteLine("InteractionType,Count");

            writer.WriteLine($"Clicks,{clicks}");
            writer.WriteLine($"Carts,{carts}");
            writer.WriteLine($"Orders,{orders}");
        }

        // ======================================
        // 2. PRODUCT POPULARITY ANALYSIS
        // ======================================

        string popularityPath = Path.Combine(
            resultsFolder,
            "product_popularity.csv");

        var popularProducts = productCounts
            .OrderByDescending(p => p.Value)
            .ToList();

        using (StreamWriter writer =
            new StreamWriter(popularityPath))
        {
            writer.WriteLine("ProductId,TotalInteractions");

            foreach (var product in popularProducts)
            {
                writer.WriteLine(
                    $"{product.Key},{product.Value}");
            }
        }

        // ======================================
        // 3. SESSION ANALYSIS
        // ======================================

        string sessionPath = Path.Combine(
            resultsFolder,
            "session_analysis.csv");

        using (StreamWriter writer =
            new StreamWriter(sessionPath))
        {
            writer.WriteLine("SessionId,TotalInteractions");

            foreach (var session in sessionCounts
                .OrderByDescending(s => s.Value))
            {
                writer.WriteLine(
                    $"{session.Key},{session.Value}");
            }
        }

        // ======================================
        // 4. OVERALL ANALYTICS SUMMARY
        // ======================================

        string summaryPath = Path.Combine(
            resultsFolder,
            "analytics_summary.txt");

        using (StreamWriter writer =
            new StreamWriter(summaryPath))
        {
            writer.WriteLine("E-COMMERCE ANALYTICS SUMMARY");
            writer.WriteLine("============================");
            writer.WriteLine();

            writer.WriteLine(
                $"Total Interactions: {totalRecords}");

            writer.WriteLine(
                $"Total Unique Products: {productCounts.Count}");

            writer.WriteLine(
                $"Total Sessions: {sessionCounts.Count}");

            writer.WriteLine();

            writer.WriteLine($"Total Clicks: {clicks}");
            writer.WriteLine($"Total Carts: {carts}");
            writer.WriteLine($"Total Orders: {orders}");

            writer.WriteLine();

            writer.WriteLine(
                "Top 10 Popular Products:");

            foreach (var product in popularProducts.Take(10))
            {
                writer.WriteLine(
                    $"Product ID: {product.Key} | " +
                    $"Interactions: {product.Value}");
            }
        }

        // ======================================
        // DISPLAY RESULTS
        // ======================================

        Console.WriteLine("\nANALYSIS COMPLETED");

        Console.WriteLine(
            "Total Interactions: " + totalRecords);

        Console.WriteLine(
            "Unique Products: " + productCounts.Count);

        Console.WriteLine(
            "Total Sessions: " + sessionCounts.Count);

        Console.WriteLine("Clicks: " + clicks);
        Console.WriteLine("Carts: " + carts);
        Console.WriteLine("Orders: " + orders);

        Console.WriteLine("\nGenerated Files:");
        Console.WriteLine(interactionPath);
        Console.WriteLine(popularityPath);
        Console.WriteLine(sessionPath);
        Console.WriteLine(summaryPath);
    }
}
