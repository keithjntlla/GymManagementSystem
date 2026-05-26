using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GymManagementSystem.Views.Settings
{
    public partial class AppearanceSettingsView : UserControl
    {
        // Selected values in the settings page (previewed state)
        private string selectedTheme = "Dark";
        private string selectedButtonStyle = "Gradient";
        private string selectedAccentColor = "Orange";

        // Mapped color palette definition
        private readonly Dictionary<string, (string solid, string gradStart, string gradEnd)> colorPalettes = 
            new Dictionary<string, (string solid, string gradStart, string gradEnd)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Orange", ("#e05e00", "#ff6b00", "#ff0000") },
                { "Blue",   ("#1b72e8", "#1e88e5", "#1565c0") },
                { "Green",  ("#0f9d58", "#2e7d32", "#1b5e20") },
                { "Purple", ("#8e24aa", "#ab47bc", "#7b1fa2") },
                { "Pink",   ("#d81b60", "#ec407a", "#c2185b") },
                { "Cyan",   ("#00acc1", "#26c6da", "#0097a7") },
                { "Red",    ("#c62828", "#e53935", "#b71c1c") },
                { "Indigo", ("#3949ab", "#5c6bc0", "#283593") }
            };

        public AppearanceSettingsView()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var settings = DatabaseHelper.GetAppearanceSettings();
                selectedTheme = settings.theme;
                selectedButtonStyle = settings.buttonStyle;
                selectedAccentColor = settings.accentColor;

                // Sync the UI controls and live preview with these saved settings
                UpdateUiSelections();
                UpdateLivePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading appearance settings: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- SELECTION EVENT HANDLERS ---

        private void ThemeDark_Click(object sender, MouseButtonEventArgs e)
        {
            selectedTheme = "Dark";
            UpdateUiSelections();
            UpdateLivePreview();
        }

        private void ThemeLight_Click(object sender, MouseButtonEventArgs e)
        {
            selectedTheme = "Light";
            UpdateUiSelections();
            UpdateLivePreview();
        }

        private void StyleGradient_Click(object sender, MouseButtonEventArgs e)
        {
            selectedButtonStyle = "Gradient";
            UpdateUiSelections();
            UpdateLivePreview();
        }

        private void StyleSolid_Click(object sender, MouseButtonEventArgs e)
        {
            selectedButtonStyle = "Solid";
            UpdateUiSelections();
            UpdateLivePreview();
        }

        private void ColorOrange_Click(object sender, MouseButtonEventArgs e) { SelectColor("Orange"); }
        private void ColorBlue_Click(object sender, MouseButtonEventArgs e) { SelectColor("Blue"); }
        private void ColorGreen_Click(object sender, MouseButtonEventArgs e) { SelectColor("Green"); }
        private void ColorPurple_Click(object sender, MouseButtonEventArgs e) { SelectColor("Purple"); }
        private void ColorPink_Click(object sender, MouseButtonEventArgs e) { SelectColor("Pink"); }
        private void ColorCyan_Click(object sender, MouseButtonEventArgs e) { SelectColor("Cyan"); }
        private void ColorRed_Click(object sender, MouseButtonEventArgs e) { SelectColor("Red"); }
        private void ColorIndigo_Click(object sender, MouseButtonEventArgs e) { SelectColor("Indigo"); }

        private void SelectColor(string colorName)
        {
            selectedAccentColor = colorName;
            UpdateUiSelections();
            UpdateLivePreview();
        }

        // --- UI DRAWING & LOGIC RENDERING ---

        private void UpdateUiSelections()
        {
            var orangeAccentBrush = (Brush)Application.Current.Resources["AppAccentBrush"];

            // 1. Theme selectors visual states
            if (selectedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                btnThemeDark.BorderBrush = orangeAccentBrush;
                btnThemeDark.BorderThickness = new Thickness(2);
                chkDarkTheme.Visibility = Visibility.Visible;
                txtDarkThemeCheck.Visibility = Visibility.Visible;

                btnThemeLight.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d30"));
                btnThemeLight.BorderThickness = new Thickness(1);
                chkLightTheme.Visibility = Visibility.Collapsed;
                txtLightThemeCheck.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnThemeLight.BorderBrush = orangeAccentBrush;
                btnThemeLight.BorderThickness = new Thickness(2);
                chkLightTheme.Visibility = Visibility.Visible;
                txtLightThemeCheck.Visibility = Visibility.Visible;

                btnThemeDark.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d30"));
                btnThemeDark.BorderThickness = new Thickness(1);
                chkDarkTheme.Visibility = Visibility.Collapsed;
                txtDarkThemeCheck.Visibility = Visibility.Collapsed;
            }

            // 2. Button style selectors visual states
            if (selectedButtonStyle.Equals("Gradient", StringComparison.OrdinalIgnoreCase))
            {
                btnStyleGradient.BorderBrush = orangeAccentBrush;
                btnStyleGradient.BorderThickness = new Thickness(2);
                chkStyleGradient.Visibility = Visibility.Visible;
                txtStyleGradientCheck.Visibility = Visibility.Visible;

                btnStyleSolid.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d30"));
                btnStyleSolid.BorderThickness = new Thickness(1);
                chkStyleSolid.Visibility = Visibility.Collapsed;
                txtStyleSolidCheck.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnStyleSolid.BorderBrush = orangeAccentBrush;
                btnStyleSolid.BorderThickness = new Thickness(2);
                chkStyleSolid.Visibility = Visibility.Visible;
                txtStyleSolidCheck.Visibility = Visibility.Visible;

                btnStyleGradient.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d30"));
                btnStyleGradient.BorderThickness = new Thickness(1);
                chkStyleGradient.Visibility = Visibility.Collapsed;
                txtStyleGradientCheck.Visibility = Visibility.Collapsed;
            }

            // Get current active colors
            var palette = colorPalettes.ContainsKey(selectedAccentColor) ? colorPalettes[selectedAccentColor] : colorPalettes["Orange"];
            var solidColor = (Color)ColorConverter.ConvertFromString(palette.solid);
            var startColor = (Color)ColorConverter.ConvertFromString(palette.gradStart);
            var endColor = (Color)ColorConverter.ConvertFromString(palette.gradEnd);

            // Update sample buttons inside selectors to match the selected color!
            btnStyleGradColorStart.Color = startColor;
            btnStyleGradColorEnd.Color = endColor;
            btnStyleSolidSample.Background = new SolidColorBrush(solidColor);

            // 3. Accent color cards active highlights (white border and tick symbol)
            var accentCardList = new Dictionary<string, (Border card, TextBlock label)>
            {
                { "Orange", (colorOrange, lblOrange) },
                { "Blue",   (colorBlue,   lblBlue) },
                { "Green",  (colorGreen,  lblGreen) },
                { "Purple", (colorPurple, lblPurple) },
                { "Pink",   (colorPink,   lblPink) },
                { "Cyan",   (colorCyan,   lblCyan) },
                { "Red",    (colorRed,    lblRed) },
                { "Indigo", (colorIndigo, lblIndigo) }
            };

            foreach (var item in accentCardList)
            {
                var cardName = item.Key;
                var card = item.Value.card;
                var label = item.Value.label;

                if (cardName.Equals(selectedAccentColor, StringComparison.OrdinalIgnoreCase))
                {
                    card.BorderBrush = new SolidColorBrush(Colors.White);
                    card.BorderThickness = new Thickness(1.5);
                    label.Text = cardName + " ✓";
                    label.Foreground = orangeAccentBrush;
                    label.FontWeight = FontWeights.Bold;
                }
                else
                {
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222225"));
                    card.BorderThickness = new Thickness(1);
                    label.Text = cardName;
                    label.Foreground = new SolidColorBrush(Colors.White);
                    label.FontWeight = FontWeights.Medium;
                }
            }
        }

        private void UpdateLivePreview()
        {
            var palette = colorPalettes.ContainsKey(selectedAccentColor) ? colorPalettes[selectedAccentColor] : colorPalettes["Orange"];
            var solidColor = (Color)ColorConverter.ConvertFromString(palette.solid);
            var startColor = (Color)ColorConverter.ConvertFromString(palette.gradStart);
            var endColor = (Color)ColorConverter.ConvertFromString(palette.gradEnd);

            // 1. Render action button style in preview
            Brush buttonBrush;
            if (selectedButtonStyle.Equals("Gradient", StringComparison.OrdinalIgnoreCase))
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                gradient.GradientStops.Add(new GradientStop(startColor, 0.0));
                gradient.GradientStops.Add(new GradientStop(endColor, 1.0));
                buttonBrush = gradient;
            }
            else
            {
                buttonBrush = new SolidColorBrush(solidColor);
            }
            previewActionButton.Background = buttonBrush;

            // 2. Render Light Theme preview vs Dark Theme preview (Dashboard elements)
            if (selectedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                // Full light card background
                previewOuterContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                previewOuterContainer.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e1e4e8"));

                previewHeaderTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a191b"));
                previewHeaderDesc.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6a737d"));

                // Light theme card content styles (standard static)
                var lightCardBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f6f8fa"));
                var lightCardBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e1e4e8"));

                var darkText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292e"));
                var lightText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#586069"));

                previewCard1.Background = lightCardBg;
                previewCard1.BorderBrush = lightCardBorder;
                previewCardTitle1.Foreground = lightText;
                previewCardValue1.Foreground = darkText;

                previewCard2.Background = lightCardBg;
                previewCard2.BorderBrush = lightCardBorder;
                previewCardTitle2.Foreground = lightText;
                previewCardValue2.Foreground = darkText;

                previewCard3.Background = lightCardBg;
                previewCard3.BorderBrush = lightCardBorder;
                previewCardTitle3.Foreground = lightText;
                previewCardValue3.Foreground = darkText;
            }
            else
            {
                // Full dark card background
                previewOuterContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a191b"));
                previewOuterContainer.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d30"));

                previewHeaderTitle.Foreground = new SolidColorBrush(Colors.White);
                previewHeaderDesc.Foreground = new SolidColorBrush(Colors.Gray);

                // Dark theme card content styles (standard static)
                var darkCardBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2a2c"));
                var darkCardBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));

                var whiteText = new SolidColorBrush(Colors.White);
                var grayText = new SolidColorBrush(Colors.Gray);

                previewCard1.Background = darkCardBg;
                previewCard1.BorderBrush = darkCardBorder;
                previewCardTitle1.Foreground = grayText;
                previewCardValue1.Foreground = whiteText;

                previewCard2.Background = darkCardBg;
                previewCard2.BorderBrush = darkCardBorder;
                previewCardTitle2.Foreground = grayText;
                previewCardValue2.Foreground = whiteText;

                previewCard3.Background = darkCardBg;
                previewCard3.BorderBrush = darkCardBorder;
                previewCardTitle3.Foreground = grayText;
                previewCardValue3.Foreground = whiteText;
            }
        }

        // --- BUTTON ACTIONS ---

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Persist selection directly into database
                DatabaseHelper.SaveAppearanceSettings(selectedTheme, selectedButtonStyle, selectedAccentColor);

                // Instantly update the application's global colors dynamically!
                App.ApplyAppearanceResources(selectedTheme, selectedButtonStyle, selectedAccentColor);

                MessageBox.Show("Appearance settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving appearance settings: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert selector choices back to saved settings
            LoadCurrentSettings();
        }
    }
}
