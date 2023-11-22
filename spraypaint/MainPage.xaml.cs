using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace spraypaint;


public partial class MainPage : ContentPage
{

    public MainPage()
    {
        InitializeComponent();
    }

    private SKColor _currentPaintColor = SKColors.Black;
    private float _currentSize;
    private byte _currentOpacity;
    private bool _isEraserMode = false;
    private SKBitmap _bitmap;
    private SKBitmap _paintBitmap;
    private bool _isDrawingMode = false;
    private Button _selectedColorButton = null;

    private async void OnOpenImageClicked(object sender, EventArgs e)
    {
        var pickOptions = new PickOptions
        {
            PickerTitle = "Please select an image",
            FileTypes = FilePickerFileType.Images
        };

        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null)
            {
                using (var stream = File.OpenRead(result.FullPath))
                {
                    _bitmap = SKBitmap.Decode(stream);
                }

                // Trigger a redraw of the canvas
                canvasView.InvalidateSurface();

            }
        }
        catch (Exception ex)
        {
            // Handle exceptions
            Console.WriteLine($"Error picking file: {ex.Message}");
        }

        if (_bitmap != null)
        {
            _paintBitmap = new SKBitmap(_bitmap.Width, _bitmap.Height);
        }
    }

    private async void OnSaveAsClicked(object sender, EventArgs e)
    {
        try
        {
            string fileName = await DisplayPromptAsync("Save As", "Enter file name:");
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName + ".png");

                using (var combinedBitmap = new SKBitmap(_bitmap.Width, _bitmap.Height))
                {
                    using (var canvas = new SKCanvas(combinedBitmap))
                    {
                        canvas.DrawBitmap(_bitmap, 0, 0);

                    }

                    using (var fileStream = File.OpenWrite(filePath))
                    {
                        combinedBitmap.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                    }
                }

                await DisplayAlert("Success", "Image saved successfully to " + filePath, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to save image: " + ex.Message, "OK");
        }
    }



    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        AdjustCanvasViewSize();
    }

    private void AdjustCanvasViewSize()
    {
        if (_bitmap == null)
            return;

        var imageViewSize = new Size(imageView.Width, imageView.Height);
        var imageAspectRatio = (float)_bitmap.Width / _bitmap.Height;

        var canvasWidth = imageViewSize.Width;
        var canvasHeight = canvasWidth / imageAspectRatio;

        canvasView.WidthRequest = canvasWidth;
        canvasView.HeightRequest = canvasHeight;
    }


    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Draw the original image
        if (_bitmap != null && _paintBitmap != null)
        {
            // Maintain the aspect ratio of the original image
            float scale = Math.Min((float)e.Info.Width / _bitmap.Width, (float)e.Info.Height / _bitmap.Height);
            float x = (e.Info.Width - scale * _bitmap.Width) / 2;
            float y = (e.Info.Height - scale * _bitmap.Height) / 2;
            canvas.DrawBitmap(_bitmap, new SKRect(x, y, x + scale * _bitmap.Width, y + scale * _bitmap.Height));
            canvas.DrawBitmap(_paintBitmap, new SKRect(x, y, x + scale * _bitmap.Width, y + scale * _bitmap.Height));
        }

    }


    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
        if (_paintBitmap != null)
        {
            if (e.ActionType == SKTouchAction.Pressed || e.ActionType == SKTouchAction.Moved)
            {
                // Adjust the touch point to the scale and position of the image
                float canvasScale = Math.Min(canvasView.CanvasSize.Width / _bitmap.Width, canvasView.CanvasSize.Height / _bitmap.Height);
                float adjustedX = (e.Location.X - (canvasView.CanvasSize.Width - _bitmap.Width * canvasScale) / 2) / canvasScale;
                float adjustedY = (e.Location.Y - (canvasView.CanvasSize.Height - _bitmap.Height * canvasScale) / 2) / canvasScale;

                DrawOnCanvas(new SKPoint(adjustedX, adjustedY));
                e.Handled = true;
            }

            if (e.Handled)
            {
                ((SKCanvasView)sender).InvalidateSurface();
            }
        }
    }


    private void DrawOnCanvas(SKPoint point)
    {
        // Ensure paint bitmap is initialized
        if (_paintBitmap == null)
        {
            _paintBitmap = new SKBitmap(_bitmap.Width, _bitmap.Height);
        }

        using (var canvas = new SKCanvas(_paintBitmap))
        {
            SKPaint paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                StrokeWidth = _currentSize
            };

            if (_isEraserMode)
            {
                // Eraser functionality
                paint.Color = SKColors.Transparent;
                paint.BlendMode = SKBlendMode.Clear; // Clear mode for eraser
                canvas.DrawCircle(point, _currentSize, paint);

            }
            else
            {
                // Normal spray paint functionality
                // _currentOpacity = (byte)(((int)_currentOpacity / 100) * 255);
                paint.Color = _currentPaintColor.WithAlpha(_currentOpacity);
                Random rand = new Random();
                int dots = 100; // Number of dots to create the spray effect

                for (int i = 0; i < dots; i++)
                {
                    float x = point.X + (float)(rand.NextDouble() - 0.5) * _currentSize * 2;
                    float y = point.Y + (float)(rand.NextDouble() - 0.5) * _currentSize * 2;
                    canvas.DrawCircle(x, y, 2, paint);
                }
            }
        }
    }




    private void SprayPaint_Clicked(object sender, EventArgs e)
    {
        _isEraserMode = false;
        _isDrawingMode = true;
        UpdateButtonAppearance(sender as Button);
    }

    private void Eraser_Clicked(object sender, EventArgs e)
    {
        _isEraserMode = true;
        _isDrawingMode = false;
        UpdateButtonAppearance(sender as Button);
    }

    private void ColorButton_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            _currentPaintColor = button.BackgroundColor.ToSKColor();
            UpdateColorButtonAppearance(button);
        }

    }

    private void SizeSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        _currentSize = (float)e.NewValue;
    }

    private void OpacitySlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        _currentOpacity = (byte)e.NewValue;
    }

    private void UpdateButtonAppearance(Button activeButton)
    {
        // Reset the appearance of all buttons
        PaintButton.BackgroundColor = Colors.Transparent;
        EraserButton.BackgroundColor = Colors.Transparent;

        // Highlight the active button
        if (activeButton != null)
        {
            activeButton.BackgroundColor = Colors.LightGray; // Or any other color to indicate selection
        }
    }

    private void UpdateColorButtonAppearance(Button selectedButton)
    {
        // Reset the appearance of previously selected button
        if (_selectedColorButton != null)
        {
            _selectedColorButton.BorderColor = Colors.Transparent;
            _selectedColorButton.BorderWidth = 0;
        }

        // Highlight the newly selected button
        if (selectedButton != null)
        {
            selectedButton.BorderColor = Colors.Gray; // Or any color to indicate selection
            selectedButton.BorderWidth = 4; // Adjust as needed
        }

        // Update the currently selected button
        _selectedColorButton = selectedButton;
    }

}