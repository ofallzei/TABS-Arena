using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TABS
{
    internal static class PlayerPanelText
    {
        internal const double ButtonFontSize = 14;
        internal const double PriceFontSize = 15;
        internal const double StatLabelFontSize = 12;
        internal const double StatValueFontSize = 19;

        internal static void ApplyButtonTypography(params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null) continue;

                button.FontSize = ButtonFontSize;
                button.FontWeight = FontWeights.SemiBold;
            }
        }

        internal static void ApplyTextSize(double fontSize, params TextBlock[] textBlocks)
        {
            foreach (TextBlock textBlock in textBlocks)
            {
                if (textBlock == null) continue;
                textBlock.FontSize = fontSize;
            }
        }

        internal static void SetButtonContent(Button button, string text)
        {
            if (button == null) return;
            button.Content = CreateButtonContent(text);
        }

        internal static FrameworkElement CreateButtonContent(string text)
        {
            text = text ?? "";

            int priceStart;
            int priceEnd;
            if (!TryFindPriceSegment(text, out priceStart, out priceEnd))
                return CreateTextSegment(text);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (priceStart > 0)
                panel.Children.Add(CreateTextSegment(text.Substring(0, priceStart)));

            panel.Children.Add(CreateOutlinedPrice(text.Substring(priceStart, priceEnd - priceStart + 1)));

            if (priceEnd + 1 < text.Length)
                panel.Children.Add(CreateTextSegment(text.Substring(priceEnd + 1)));

            return panel;
        }

        internal static InlineUIContainer CreateFlagInline(Brush flagBrush, double width, double height, Thickness margin)
        {
            var canvas = new Canvas
            {
                Width = 32,
                Height = 36,
                SnapsToDevicePixels = true
            };

            var pole = new System.Windows.Shapes.Rectangle
            {
                Width = 5.8,
                Height = 33,
                RadiusX = 1.6,
                RadiusY = 1.6,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1.8
            };
            Canvas.SetLeft(pole, 1.4);
            Canvas.SetTop(pole, 1.5);
            canvas.Children.Add(pole);

            var flag = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 6.4 4.2 C 12.6 7.1 18.9 1.8 29.2 4.1 C 30.5 4.4 31.0 6.0 29.8 6.9 L 23.0 12.4 L 29.5 18.8 C 30.6 19.9 29.8 21.4 28.3 20.9 C 20.1 18.5 13.3 22.5 6.4 19.0 Z"),
                Fill = flagBrush,
                Stroke = Brushes.Black,
                StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(flag);

            return new InlineUIContainer(new Viewbox
            {
                Width = width,
                Height = height,
                Margin = margin,
                Stretch = Stretch.Fill,
                Child = canvas
            })
            {
                BaselineAlignment = BaselineAlignment.Center
            };
        }

        internal static InlineUIContainer CreateOutlinedTextInline(string text, double fontSize, Thickness margin)
        {
            return CreateOutlinedTextInline(text, fontSize, margin, Brushes.White);
        }

        internal static InlineUIContainer CreateOutlinedTextInline(string text, double fontSize, Thickness margin, Brush foreground)
        {
            return new InlineUIContainer(CreateOutlinedText(text, fontSize, margin, foreground))
            {
                BaselineAlignment = BaselineAlignment.Center
            };
        }

        private static TextBlock CreateTextSegment(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = ButtonFontSize,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Grid CreateOutlinedPrice(string text)
        {
            return CreateOutlinedText(text, PriceFontSize, new Thickness(1, 0, 1, 0), Brushes.White);
        }

        private static Grid CreateOutlinedText(string text, double fontSize, Thickness margin, Brush foreground)
        {
            var grid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin
            };

            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, -1, -1);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, 0, -1);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, 1, -1);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, -1, 0);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, 1, 0);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, -1, 1);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, 0, 1);
            AddOutlinedTextLayer(grid, text, fontSize, Brushes.Black, 1, 1);
            AddOutlinedTextLayer(grid, text, fontSize, foreground, 0, 0);

            return grid;
        }

        private static void AddOutlinedTextLayer(Grid grid, string text, double fontSize, Brush foreground, double offsetX, double offsetY)
        {
            grid.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new TranslateTransform(offsetX, offsetY)
            });

        }

        private static bool TryFindPriceSegment(string text, out int start, out int end)
        {
            start = -1;
            end = -1;

            int searchStart = text.LastIndexOf('(');
            while (searchStart >= 0)
            {
                int close = text.IndexOf(')', searchStart);
                if (close > searchStart && ContainsDigit(text, searchStart + 1, close))
                {
                    start = searchStart;
                    end = close;
                    return true;
                }

                searchStart = searchStart > 0 ? text.LastIndexOf('(', searchStart - 1) : -1;
            }

            return false;
        }

        private static bool ContainsDigit(string text, int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (char.IsDigit(text[i]))
                    return true;
            }

            return false;
        }
    }
}
