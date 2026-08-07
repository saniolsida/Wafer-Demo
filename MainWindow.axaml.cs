using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OpenCvSharp;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using Window = Avalonia.Controls.Window;

namespace VisionInspectionDemo;

public sealed partial class MainWindow : Window
{
    private readonly Image _originalView;
    private readonly Image _grayView;
    private readonly Image _binaryView;
    private readonly ComboBox _detectionModeInput;
    private readonly NumericUpDown _thresholdInput;
    private readonly NumericUpDown _hueMinimumInput;
    private readonly NumericUpDown _hueMaximumInput;
    private readonly NumericUpDown _saturationMinimumInput;
    private readonly NumericUpDown _valueMinimumInput;
    private readonly NumericUpDown _minimumAreaInput;
    private readonly NumericUpDown _failDefectCountInput;
    private readonly NumericUpDown _failAreaRatioInput;
    private readonly NumericUpDown _failLargestAreaInput;
    private readonly CheckBox _circularRoiInput;
    private readonly TextBlock _metricsLabel;
    private readonly TextBlock _judgmentLabel;
    private readonly TextBlock _reasonLabel;

    private Mat? _source;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        _originalView = this.FindControl<Image>("OriginalView")!;
        _grayView = this.FindControl<Image>("GrayView")!;
        _binaryView = this.FindControl<Image>("BinaryView")!;
        _detectionModeInput = this.FindControl<ComboBox>("DetectionModeInput")!;
        _thresholdInput = this.FindControl<NumericUpDown>("ThresholdInput")!;
        _hueMinimumInput = this.FindControl<NumericUpDown>("HueMinimumInput")!;
        _hueMaximumInput = this.FindControl<NumericUpDown>("HueMaximumInput")!;
        _saturationMinimumInput = this.FindControl<NumericUpDown>("SaturationMinimumInput")!;
        _valueMinimumInput = this.FindControl<NumericUpDown>("ValueMinimumInput")!;
        _minimumAreaInput = this.FindControl<NumericUpDown>("MinimumAreaInput")!;
        _failDefectCountInput = this.FindControl<NumericUpDown>("FailDefectCountInput")!;
        _failAreaRatioInput = this.FindControl<NumericUpDown>("FailAreaRatioInput")!;
        _failLargestAreaInput = this.FindControl<NumericUpDown>("FailLargestAreaInput")!;
        _circularRoiInput = this.FindControl<CheckBox>("CircularRoiInput")!;
        _metricsLabel = this.FindControl<TextBlock>("MetricsLabel")!;
        _judgmentLabel = this.FindControl<TextBlock>("JudgmentLabel")!;
        _reasonLabel = this.FindControl<TextBlock>("ReasonLabel")!;
    }

    private async void LoadImage_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "검사 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter =
            [
                FilePickerFileTypes.ImageAll,
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            await LoadImageAsync(files[0]);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("이미지 불러오기 실패", ex.Message);
        }
    }

    private void Window_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void Window_Drop(object? sender, DragEventArgs e)
    {
        var file = e.Data.GetFiles()?.OfType<IStorageFile>().FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await LoadImageAsync(file);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("이미지 불러오기 실패", ex.Message);
        }
    }

    private async Task LoadImageAsync(IStorageFile file)
    {
        await using var input = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);

        var loaded = Cv2.ImDecode(memory.ToArray(), ImreadModes.Color);
        if (loaded.Empty())
        {
            loaded.Dispose();
            throw new InvalidDataException("OpenCV가 이미지를 해석하지 못했습니다.");
        }

        _source?.Dispose();
        _source = loaded;
        SetImage(_originalView, ToBitmap(_source));
        SetImage(_grayView, null);
        SetImage(_binaryView, null);
        _metricsLabel.Text = "결함 수: - | 전체 면적: - | 면적 비율: - | 최대 결함: -";
        _reasonLabel.Text = "";
        SetJudgment("READY", "#666666");
    }

    private async void Inspect_Click(object? sender, RoutedEventArgs e)
    {
        if (_source is null)
        {
            await ShowMessageAsync("검사", "먼저 이미지를 불러오세요.");
            return;
        }

        try
        {
            var hueMinimum = (int)(_hueMinimumInput.Value ?? 20);
            var hueMaximum = (int)(_hueMaximumInput.Value ?? 40);
            if (hueMinimum > hueMaximum)
            {
                await ShowMessageAsync("검사 설정", "HSV H 최소값은 최대값보다 클 수 없습니다.");
                return;
            }

            using var result = InspectionProcessor.Run(
                _source,
                _detectionModeInput.SelectedIndex == 0
                    ? DetectionMode.YellowHsv
                    : DetectionMode.GrayThreshold,
                (int)(_thresholdInput.Value ?? 170),
                hueMinimum,
                hueMaximum,
                (int)(_saturationMinimumInput.Value ?? 100),
                (int)(_valueMinimumInput.Value ?? 100),
                (double)(_minimumAreaInput.Value ?? 5),
                _circularRoiInput.IsChecked == true);

            SetImage(_originalView, ToBitmap(result.Overlay));
            SetImage(_grayView, ToBitmap(result.Gray));
            SetImage(_binaryView, ToBitmap(result.Binary));

            var failDefectCount = (int)(_failDefectCountInput.Value ?? 20);
            var failAreaRatio = (double)(_failAreaRatioInput.Value ?? 5) / 100.0;
            var failLargestArea = (double)(_failLargestAreaInput.Value ?? 500);
            var failReasons = new List<string>();

            if (result.DefectCount >= failDefectCount)
            {
                failReasons.Add($"결함 수 {result.DefectCount:N0} ≥ {failDefectCount:N0}");
            }

            if (result.DefectAreaRatio >= failAreaRatio)
            {
                failReasons.Add($"면적 비율 {result.DefectAreaRatio:P2} ≥ {failAreaRatio:P2}");
            }

            if (result.LargestDefectArea >= failLargestArea)
            {
                failReasons.Add($"최대 결함 {result.LargestDefectArea:N0}px² ≥ {failLargestArea:N0}px²");
            }

            var passed = failReasons.Count == 0;
            _metricsLabel.Text =
                $"결함 수: {result.DefectCount:N0}  |  " +
                $"전체 면적: {result.TotalDefectArea:N0}px²  |  " +
                $"면적 비율: {result.DefectAreaRatio:P2}  |  " +
                $"최대 결함: {result.LargestDefectArea:N0}px²";
            _reasonLabel.Text = passed
                ? "모든 판정 항목이 설정 기준 미만입니다."
                : "FAIL 사유: " + string.Join(" / ", failReasons);
            SetJudgment(passed ? "PASS" : "FAIL", passed ? "#228B22" : "#B22222");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("검사 실패", ex.Message);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.Dispose();
        _source = null;
        SetImage(_originalView, null);
        SetImage(_grayView, null);
        SetImage(_binaryView, null);
        base.OnClosed(e);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var closeButton = new Button
        {
            Content = "확인",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            MinWidth = 90
        };
        var dialog = new Window
        {
            Title = title,
            Width = 430,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(22),
                Spacing = 22,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    closeButton
                }
            }
        };
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private void SetJudgment(string text, string color)
    {
        _judgmentLabel.Text = text;
        _judgmentLabel.Foreground = Brush.Parse(color);
    }

    private static AvaloniaBitmap ToBitmap(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        using var stream = new MemoryStream(bytes);
        return new AvaloniaBitmap(stream);
    }

    private static void SetImage(Image view, AvaloniaBitmap? next)
    {
        var previous = view.Source as IDisposable;
        view.Source = next;
        previous?.Dispose();
    }
}
