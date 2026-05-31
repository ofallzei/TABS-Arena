using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TABS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            EventManager.RegisterClassHandler(
                typeof(ButtonBase),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(PlayButtonClick),
                true);

            EventManager.RegisterClassHandler(
                typeof(Slider),
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(SoundSlider_PreviewMouseLeftButtonDown),
                true);

            EventManager.RegisterClassHandler(
                typeof(Slider),
                UIElement.PreviewMouseMoveEvent,
                new MouseEventHandler(SoundSlider_PreviewMouseMove),
                true);

            EventManager.RegisterClassHandler(
                typeof(Slider),
                UIElement.PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(SoundSlider_PreviewMouseLeftButtonUp),
                true);
        }

        private static void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Name == "SettingsSoundsToggleButton")
                return;

            if (sender is DependencyObject source && IsInsideSlider(source))
                return;

            AudioFeedback.PlayButtonClick();
        }

        private static void SoundSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Slider slider) || slider.Name != "SettingsSoundVolumeSlider")
                return;

            SetSliderValueFromPoint(slider, e.GetPosition(slider));
            slider.CaptureMouse();
            e.Handled = true;
        }

        private static void SoundSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!(sender is Slider slider) ||
                slider.Name != "SettingsSoundVolumeSlider" ||
                !slider.IsMouseCaptured ||
                e.LeftButton != MouseButtonState.Pressed)
                return;

            SetSliderValueFromPoint(slider, e.GetPosition(slider));
            e.Handled = true;
        }

        private static void SoundSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Slider slider) || slider.Name != "SettingsSoundVolumeSlider")
                return;

            if (slider.IsMouseCaptured)
                slider.ReleaseMouseCapture();

            e.Handled = true;
        }

        private static void SetSliderValueFromPoint(Slider slider, Point point)
        {
            double percent;
            if (slider.Orientation == Orientation.Vertical)
                percent = 1.0 - (point.Y / slider.ActualHeight);
            else
                percent = point.X / slider.ActualWidth;

            percent = Clamp(percent, 0.0, 1.0);
            slider.Value = slider.Minimum + ((slider.Maximum - slider.Minimum) * percent);
        }

        private static bool IsInsideSlider(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is Slider)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
