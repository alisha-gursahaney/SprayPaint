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

    private SKColor _currentPaintColor = SKColors.White;
    private float _currentSize;
    private byte _currentOpacity;
    private bool _isEraserMode = false;
    private SKBitmap _bitmap;
    private SKBitmap _paintBitmap;
    private Button _selectedColorButton = null;

    private async void OnOpenImageClicked(object sender, EventArgs e)
    {

        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null)
            {
                using (var stream = File.OpenRead(result.FullPath))
                {
                    _bitmap = SKBitmap.Decode(stream);
                }
                canvasView.InvalidateSurface();

            }
        }
        catch (Exception ex)
        {
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


    private async void OnSavePaintOnlyClicked(object sender, EventArgs e)
    {
        try
        {
            string fileName = await DisplayPromptAsync("Save Paint", "Enter file name for the paint layer:");
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName + "_paint.png");

                if (_paintBitmap != null)
                {
                    using (var fileStream = File.OpenWrite(filePath))
                    {
                        _paintBitmap.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                    }

                    await DisplayAlert("Success", "Paint layer saved successfully to " + filePath, "OK");
                }
                else
                {
                    await DisplayAlert("Info", "There is no paint to save.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to save paint layer: " + ex.Message, "OK");
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

        if (_bitmap != null && _paintBitmap != null)
        {
            float scale = Math.Min((float)e.Info.Width / _bitmap.Width, (float)e.Info.Height / _bitmap.Height);
            float x = (e.Info.Width - scale * _bitmap.Width) / 2;
            float y = (e.Info.Height - scale * _bitmap.Height) / 2;
            canvas.DrawBitmap(_bitmap, new SKRect(x, y, x + scale * _bitmap.Width, y + scale * _bitmap.Height));
            canvas.DrawBitmap(_paintBitmap, new SKRect(x, y, x + scale * _bitmap.Width, y + scale * _bitmap.Height));
        }

    }


    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
        var canvasBounds = new SKRect(0, 0, canvasView.CanvasSize.Width, canvasView.CanvasSize.Height);
        if (canvasBounds.Contains(e.Location))
        {
            if (_paintBitmap != null)
            {
                if (e.ActionType == SKTouchAction.Pressed || e.ActionType == SKTouchAction.Moved)
                {
                    // Adjust touch points to image
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
                // Paint transparent over top to simulate erasing
                paint.Color = SKColors.Transparent;
                paint.BlendMode = SKBlendMode.Clear;
                canvas.DrawCircle(point, _currentSize, paint);

            }
            else
            {
                paint.Color = _currentPaintColor.WithAlpha(_currentOpacity);
                // Spray paint effect with random dots
                Random rand = new Random();
                int dots = 100;

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
        UpdateButtonAppearance(sender as Button);
    }

    private void Eraser_Clicked(object sender, EventArgs e)
    {
        _isEraserMode = true;
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
        // max opacity is 255 but we want to keep slider values from 1-100
        _currentOpacity = (byte)(e.NewValue * 255 / 100);
    }

    private void UpdateButtonAppearance(Button activeButton)
    {
        // Reset the appearance of all buttons
        PaintButton.BackgroundColor = Colors.White;
        PaintButton.BorderColor = Colors.Transparent;
        PaintButton.BorderWidth = 0;

        EraserButton.BackgroundColor = Colors.White;
        EraserButton.BorderColor = Colors.Transparent;
        EraserButton.BorderWidth = 0;

        // Highlight the active button
        if (activeButton != null)
        {
            activeButton.BackgroundColor = Colors.LightGray;
            activeButton.BorderColor = Colors.DarkRed;
            activeButton.BorderWidth = 5;
        }
    }

    private void UpdateColorButtonAppearance(Button selectedButton)
    {
        // Reset the appearance of previously selected button
        if (_selectedColorButton != null)
        {
            _selectedColorButton.BorderColor = Colors.DarkGray;
            _selectedColorButton.BorderWidth = 1;
        }

        // Highlight the newly selected button
        if (selectedButton != null)
        {
            selectedButton.BorderColor = Colors.DarkRed;
            selectedButton.BorderWidth = 5;
        }

        // Update the currently selected button
        _selectedColorButton = selectedButton;
    }


}