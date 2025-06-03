using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Globalization;

namespace CurrencyExchanger.Utils
{
    public static class CurrencyGraphBuilder
    {
        public static Image<Rgba32> BuildComparisonChart(Dictionary<DateTime, decimal> fromRates,
            Dictionary<DateTime, decimal> toRates, string fromLabel, string toLabel)
        {
            var commonDates = fromRates.Keys.Intersect(toRates.Keys).OrderBy(d => d).ToList();

            if (commonDates.Count < 2)
                throw new InvalidOperationException("Недостатньо спільних дат для побудови графіка.");

            var fromStart = fromRates[commonDates.First()];
            var toStart = toRates[commonDates.First()];

            var fromPercents = commonDates.ToDictionary(d => d, d =>
                (float)(((double)fromRates[d] / (double)fromStart - 1.0) * 100));

            var toPercents = commonDates.ToDictionary(d => d, d =>
                (float)(((double)toRates[d] / (double)toStart - 1.0) * 100));

            float globalMin = MathF.Min(fromPercents.Values.Min(), toPercents.Values.Min());
            float globalMax = MathF.Max(fromPercents.Values.Max(), toPercents.Values.Max());

            if (MathF.Abs(globalMax - globalMin) < 0.01f)
                globalMax = globalMin + 0.01f;

            globalMin -= 0.2f;
            globalMax += 0.2f;

            var image = new Image<Rgba32>(900, 500);
            var margin = 60;
            var width = image.Width - 2 * margin;
            var height = image.Height - 2 * margin;
            var stepX = width / (commonDates.Count - 1f);

            image.Mutate(ctx =>
            {
                ctx.Fill(Color.White);
                var font = SystemFonts.CreateFont("Arial", 14);
                var smallFont = SystemFonts.CreateFont("Arial", 12);

                var fromPoints = commonDates.Select((date, i) =>
                {
                    float x = margin + i * stepX;
                    float y = (1 - ((fromPercents[date] - globalMin) / (globalMax - globalMin))) * height + margin;
                    return new PointF(x, y);
                }).ToArray();

                var toPoints = commonDates.Select((date, i) =>
                {
                    float x = margin + i * stepX;
                    float y = (1 - ((toPercents[date] - globalMin) / (globalMax - globalMin))) * height + margin;
                    return new PointF(x, y);
                }).ToArray();

                for (int i = 0; i <= 5; i++)
                {
                    float y = margin + i * (height / 5f);
                    ctx.DrawLine(Color.LightGray, 1, new PointF(margin, y), new PointF(margin + width, y));
                }
                ctx.DrawLine(Color.Red, 3, fromPoints);
                ctx.DrawLine(Color.Blue, 3, toPoints);

                for (int i = 0; i < commonDates.Count; i++)
                {
                    string label1 = $"{fromPercents[commonDates[i]]:+0.##;-0.##;0}%";
                    string label2 = $"{toPercents[commonDates[i]]:+0.##;-0.##;0}%";

                    ctx.DrawText(label1, smallFont, Color.Red, new PointF(fromPoints[i].X - 15, fromPoints[i].Y - 20));
                    ctx.DrawText(label2, smallFont, Color.Blue, new PointF(toPoints[i].X - 15, toPoints[i].Y + 5));
                }

                for (int i = 0; i < commonDates.Count; i++)
                {
                    var dateText = commonDates[i].ToString("dd.MM", CultureInfo.InvariantCulture);
                    float x = margin + i * stepX;
                    ctx.DrawText(dateText, smallFont, Color.Black, new PointF(x, image.Height - margin + 10));
                }

                ctx.DrawText(fromLabel, font, Color.Red, new PointF(margin, margin - 40));
                ctx.DrawText(toLabel, font, Color.Blue, new PointF(margin + 60, margin - 40));

                ctx.DrawText($"{fromLabel}: {fromPercents.Values.Min():+0.##;-0.##;0}%–{fromPercents.Values.Max():+0.##;-0.##;0}%", smallFont, Color.Red, new PointF(image.Width - 250, margin - 40));
                ctx.DrawText($"{toLabel}: {toPercents.Values.Min():+0.##;-0.##;0}%–{toPercents.Values.Max():+0.##;-0.##;0}%", smallFont, Color.Blue, new PointF(image.Width - 250, margin - 20));
            });

            return image;
        }

    }
}
