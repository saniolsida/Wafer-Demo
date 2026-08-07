using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

namespace VisionInspectionDemo;

internal enum DetectionMode
{
    YellowHsv,
    GrayThreshold
}

internal sealed class InspectionResult : IDisposable
{
    public required Mat Gray { get; init; }
    public required Mat Binary { get; init; }
    public required Mat Overlay { get; init; }
    public required int DefectCount { get; init; }
    public required double TotalDefectArea { get; init; }
    public required double DefectAreaRatio { get; init; }
    public required double LargestDefectArea { get; init; }

    public void Dispose()
    {
        Gray.Dispose();
        Binary.Dispose();
        Overlay.Dispose();
    }
}

internal static class InspectionProcessor
{
    public static InspectionResult Run(
        Mat source,
        DetectionMode detectionMode,
        int threshold,
        int hueMinimum,
        int hueMaximum,
        int saturationMinimum,
        int valueMinimum,
        double minimumArea,
        bool useCircularRoi)
    {
        if (source.Empty())
        {
            throw new ArgumentException("검사할 이미지가 비어 있습니다.", nameof(source));
        }

        var gray = new Mat();
        if (source.Channels() == 1)
        {
            source.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        }

        var binary = new Mat();
        if (detectionMode == DetectionMode.YellowHsv)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(source, hsv, ColorConversionCodes.BGR2HSV);
            Cv2.InRange(
                hsv,
                new Scalar(hueMinimum, saturationMinimum, valueMinimum),
                new Scalar(hueMaximum, 255, 255),
                binary);
        }
        else
        {
            Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);
        }

        double roiArea = binary.Width * binary.Height;
        if (useCircularRoi)
        {
            using var mask = Mat.Zeros(binary.Size(), MatType.CV_8UC1).ToMat();
            var center = new CvPoint(binary.Width / 2, binary.Height / 2);
            var radius = (int)(Math.Min(binary.Width, binary.Height) * 0.46);
            Cv2.Circle(mask, center, radius, Scalar.White, -1);
            Cv2.BitwiseAnd(binary, mask, binary);
            roiArea = Cv2.CountNonZero(mask);
        }

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var labelCount = Cv2.ConnectedComponentsWithStats(
            binary,
            labels,
            stats,
            centroids,
            PixelConnectivity.Connectivity8,
            MatType.CV_32S);

        var defects = new List<(int Area, Rect Bounds)>();
        for (var label = 1; label < labelCount; label++)
        {
            // ConnectedComponentsWithStats columns: left, top, width, height, area.
            var area = stats.At<int>(label, 4);
            if (area < minimumArea)
            {
                continue;
            }

            defects.Add((
                area,
                new Rect(
                    stats.At<int>(label, 0),
                    stats.At<int>(label, 1),
                    stats.At<int>(label, 2),
                    stats.At<int>(label, 3))));
        }

        var totalDefectArea = defects.Sum(defect => (double)defect.Area);
        var largestDefectArea = defects.Count == 0
            ? 0
            : defects.Max(defect => (double)defect.Area);
        var defectAreaRatio = roiArea <= 0 ? 0 : totalDefectArea / roiArea;

        var overlay = new Mat();
        if (source.Channels() == 1)
        {
            Cv2.CvtColor(source, overlay, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            source.CopyTo(overlay);
        }

        foreach (var defect in defects)
        {
            Cv2.Rectangle(overlay, defect.Bounds, new Scalar(0, 0, 255), 2);
        }

        return new InspectionResult
        {
            Gray = gray,
            Binary = binary,
            Overlay = overlay,
            DefectCount = defects.Count,
            TotalDefectArea = totalDefectArea,
            DefectAreaRatio = defectAreaRatio,
            LargestDefectArea = largestDefectArea
        };
    }
}
