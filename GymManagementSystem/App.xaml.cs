using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GymManagementSystem
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Configure default culture to enforce MM-dd-yyyy format globally
            System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en-US");
            culture.DateTimeFormat.ShortDatePattern = "MM-dd-yyyy";
            culture.DateTimeFormat.LongDatePattern = "MM-dd-yyyy";
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // Force all WPF controls (DatePicker, TextBlocks, Bindings) to honor the custom culture
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            base.OnStartup(e);
            
            try 
            {
                DatabaseHelper.InitializeDatabase();
                DatabaseHelper.RefreshMemberStatuses();
                
                // Load appearance settings on startup
                var appearance = DatabaseHelper.GetAppearanceSettings();
                ApplyAppearanceResources(appearance.theme, appearance.buttonStyle, appearance.accentColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing database: " + ex.Message);
            }
        }

        public static void ApplyAppearanceResources(string theme, string buttonStyle, string accentColor)
        {
            bool isLight = theme.Equals("Light", StringComparison.OrdinalIgnoreCase);

            // ── 1. Accent colors (lighter variants for light mode for contrast) ─────
            Color solidColor, gradStart, gradEnd;

            switch (accentColor.ToLower())
            {
                case "blue":
                    solidColor = C(isLight ? "#1558B0" : "#1b72e8");
                    gradStart  = C(isLight ? "#1669C1" : "#1e88e5");
                    gradEnd    = C(isLight ? "#0D47A1" : "#1565c0");
                    break;
                case "green":
                    solidColor = C(isLight ? "#0B7A43" : "#0f9d58");
                    gradStart  = C(isLight ? "#1B6B3A" : "#2e7d32");
                    gradEnd    = C(isLight ? "#0A4F2B" : "#1b5e20");
                    break;
                case "purple":
                    solidColor = C(isLight ? "#6A1080" : "#8e24aa");
                    gradStart  = C(isLight ? "#7B2190" : "#ab47bc");
                    gradEnd    = C(isLight ? "#4A0E5E" : "#7b1fa2");
                    break;
                case "pink":
                    solidColor = C(isLight ? "#A01248" : "#d81b60");
                    gradStart  = C(isLight ? "#B5194F" : "#ec407a");
                    gradEnd    = C(isLight ? "#880E3F" : "#c2185b");
                    break;
                case "cyan":
                    solidColor = C(isLight ? "#00839A" : "#00acc1");
                    gradStart  = C(isLight ? "#0094AD" : "#26c6da");
                    gradEnd    = C(isLight ? "#006B7E" : "#0097a7");
                    break;
                case "red":
                    solidColor = C(isLight ? "#991E1E" : "#c62828");
                    gradStart  = C(isLight ? "#B02020" : "#e53935");
                    gradEnd    = C(isLight ? "#7A1515" : "#b71c1c");
                    break;
                case "indigo":
                    solidColor = C(isLight ? "#283585" : "#3949ab");
                    gradStart  = C(isLight ? "#3545A0" : "#5c6bc0");
                    gradEnd    = C(isLight ? "#1A2470" : "#283593");
                    break;
                case "orange":
                default:
                    solidColor = C(isLight ? "#C04D00" : "#e05e00");
                    gradStart  = C(isLight ? "#D45500" : "#ff6b00");
                    gradEnd    = C(isLight ? "#A03C00" : "#ff0000");
                    break;
            }

            var accentBrush = new SolidColorBrush(solidColor);
            accentBrush.Freeze();

            var hoverBrush = new SolidColorBrush(Color.FromArgb(200, solidColor.R, solidColor.G, solidColor.B));
            hoverBrush.Freeze();

            Brush buttonBrush;
            if (buttonStyle.ToLower() == "gradient")
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint   = new Point(1, 0)
                };
                gradient.GradientStops.Add(new GradientStop(gradStart, 0.0));
                gradient.GradientStops.Add(new GradientStop(gradEnd,   1.0));
                gradient.Freeze();
                buttonBrush = gradient;
            }
            else
            {
                buttonBrush = accentBrush;
            }

            Set("AppAccentBrush",       accentBrush);
            Set("AppAccentButtonBrush", buttonBrush);
            Set("AppAccentHoverBrush",  hoverBrush);
            Set("AppAccentColor",        solidColor);

            // ── 2. Theme surface / text / border colors ──────────────────────────────
            if (isLight)
            {
                // Backgrounds
                Set("ThemeBg",              B("#F3F4F6"));
                Set("ThemeSidebarBg",       B("#F8FAFC"));
                Set("ThemeSidebarBorder",   B("#E2E6EC"));
                Set("ThemePanelBg",         B("#ECEFF3"));
                Set("ThemeTableBg",         B("#ECEFF3"));
                Set("ThemeCardBg",          B("#F8FAFC"));
                Set("ThemeCardBorder",      B("#E2E6EC"));
                Set("ThemeInputBg",         B("#F8FAFC"));
                Set("ThemeInputBorder",     B("#D0D5DD"));
                Set("ThemeInputDisabledBg", B("#F5F5F5"));

                // Text
                Set("ThemeFgPrimary",   B("#111827"));
                Set("ThemeFgSecondary", B("#6B7280"));
                Set("ThemeFgMuted",     B("#9CA3AF"));

                // Navigation (sidebar flips to clean white/light styling)
                Set("ThemeSidebarFg",      B("#5F6368"));
                Set("ThemeNavHoverBg",     B("#F1F3F4"));
                Set("ThemeNavHoverFg",     B("#111827"));
                Set("ThemeSubNavActiveBg", B("#E8F0FE"));
                Set("ThemeSubNavHoverBg",  B("#F4F6F9"));

                // Misc
                Set("ThemeScrollThumb",        B("#C1C9D4"));
                Set("ThemeDataGridAltRow",     B("#F4F6F9"));
                Set("ThemeDataGridSelected",   B("#EBF0FB"));
                Set("ThemeDataGridSelectedFg", B("#111827"));
                Set("ThemeBadgeBg",            B("#EFF0F3"));
                Set("ThemeBadgeFg",            B("#374151"));
                Set("ThemeSeparator",          B("#DDE1EA"));
                Set("ThemeDropdownBg",         B("#F8FAFC"));
                Set("ThemeDropdownBorder",     B("#D0D5DD"));
                Set("ThemeDropdownHoverBg",    B("#F0F2F5"));
                Set("ThemeComboArrow",         B("#374151"));
                Set("ThemeNavBtnBg",           B("#F8FAFC"));
                Set("ThemeNavBtnBorder",       B("#D0D5DD"));
                Set("ThemeNavBtnHoverBg",      B("#F0F2F5"));

                // Membership plan badge backgrounds (light-tinted pastels)
                Set("ThemeMemberBadgeDailyBg",   B("#FFF3E0"));
                Set("ThemeMemberBadgeWeeklyBg",  B("#E0F7FA"));
                Set("ThemeMemberBadgeHalfBg",    B("#F3E5F5"));
                Set("ThemeMemberBadgeMonthlyBg", B("#E3F2FD"));
                Set("ThemeMemberBadgeYearlyBg",  B("#E8F5E9"));
                Set("ThemeMemberBadgePromoBg",   B("#FCE4EC"));

                // ── Mockup Light Theme Card pastels ──────────────────────
                Set("ThemeCheckinsBg",           B("#E6F4EA"));
                Set("ThemeCheckinsBorder",       B("#CEEAD6"));
                Set("ThemeCheckinsFg",           B("#137333"));
                Set("ThemeCheckinsLabelFg",      B("#3C4043"));
                Set("ThemeCheckinsIconFg",       B("#137333"));

                Set("ThemeOccupancyBg",          B("#E6F4EA"));
                Set("ThemeOccupancyBorder",      B("#CEEAD6"));
                Set("ThemeOccupancyFg",          B("#137333"));
                Set("ThemeOccupancyLabelFg",     B("#3C4043"));
                Set("ThemeOccupancyIconFg",      B("#137333"));

                Set("ThemeTodayRevenueBg",       B("#EAF6EC"));
                Set("ThemeTodayRevenueBorder",   B("#CEEAD6"));
                Set("ThemeTodayRevenueFg",       B("#137333"));
                Set("ThemeTodayRevenueLabelFg",  B("#3C4043"));
                Set("ThemeTodayRevenueIconFg",   B("#137333"));

                Set("ThemeMonthlyRevenueBg",     B("#EAF6EC"));
                Set("ThemeMonthlyRevenueBorder", B("#CEEAD6"));
                Set("ThemeMonthlyRevenueFg",     B("#137333"));
                Set("ThemeMonthlyRevenueLabelFg",B("#3C4043"));
                Set("ThemeMonthlyRevenueIconFg", B("#137333"));

                Set("ThemeNewMembersBg",         B("#FEF7E0"));
                Set("ThemeNewMembersBorder",     B("#FADF7F"));
                Set("ThemeNewMembersFg",         B("#FF9940"));
                Set("ThemeNewMembersLabelFg",    B("#FF9940"));
                Set("ThemeNewMembersIconFg",     B("#FF9940"));

                // Inline check-in card
                Set("ThemeInlineAttendanceBg",          B("#F8FAFC"));
                Set("ThemeInlineAttendanceBorder",      B("#E2E6EC"));
                Set("ThemeInlineAttendanceLabelFg",     B("#3C4043"));
                Set("ThemeInlineAttendanceButtonBg",    B("#AECBFA"));
                Set("ThemeInlineAttendanceButtonBorder",B("#AECBFA"));
                Set("ThemeInlineAttendanceButtonFg",    B("#174EA6"));
                Set("ThemeInlineAttendanceButtonHoverBg",B("#8AB4F8"));

                // Expiration report urgency rows
                Set("ThemeExpiredRowBg",                B("#FCE8E6"));
                Set("ThemeCriticalRowBg",               B("#FEF3E2"));
                Set("ThemeWarningRowBg",                B("#FFF8D7"));

                // Alerts Fg
                Set("ThemeAlertRedFg",           B("#C5221F"));
                Set("ThemeAlertOrangeFg",        B("#FF9940"));

                // Payment badges
                Set("ThemeBadgeCashBg",          B("#E6F4EA"));
                Set("ThemeBadgeCashFg",          B("#137333"));
                Set("ThemeBadgeGCashBg",         B("#E8F0FE"));
                Set("ThemeBadgeGCashFg",         B("#174EA6"));
                Set("ThemeBadgeRefundBg",        B("#FCE8E6"));
                Set("ThemeBadgeRefundFg",        B("#C5221F"));

                // Specializations
                Set("ThemeSpecializationBadgeBg",     B("#FFF3E0"));
                Set("ThemeSpecializationBadgeFg",     B("#E65100"));
                Set("ThemeSpecializationBadgeBorder", B("Transparent"));

                // Discount / Promo badges
                Set("ThemeDiscountBadgeBg",      B("#F3E5F5"));
                Set("ThemeDiscountBadgeFg",      B("#7B1FA2"));

                // Role & Status Badges
                Set("ThemeRoleAdminBg",       B("#F3E5F5"));
                Set("ThemeRoleAdminFg",       B("#7B1FA2"));
                Set("ThemeRoleStaffBg",       B("#E3F2FD"));
                Set("ThemeRoleStaffFg",       B("#1565C0"));
                Set("ThemeStatusActiveBg",    B("#E6F4EA"));
                Set("ThemeStatusActiveFg",    B("#137333"));
                Set("ThemeStatusInactiveBg",  B("#FCE8E6"));
                Set("ThemeStatusInactiveFg",  B("#C5221F"));

                // Refunds & Audits
                Set("ThemeRefundReasonFg",    B("#000000"));
                Set("ThemeAuditHeaderFg",     B("#000000"));

                // Info/Note alert boxes
                Set("ThemeNoteAlertBg",       B("#E8F0FE"));
                Set("ThemeNoteAlertFg",       B("#1967D2"));
                Set("ThemeNoteAlertIconFg",   B("#1A73E8"));

                // Soft Shadows for Light Mode
                var cardShadow = new DropShadowEffect
                {
                    BlurRadius = 15,
                    Direction = 270,
                    ShadowDepth = 4,
                    Opacity = 0.06,
                    Color = Colors.Black
                };
                cardShadow.Freeze();
                Set("ThemeCardShadow", cardShadow);

                var tableShadow = new DropShadowEffect
                {
                    BlurRadius = 20,
                    Direction = 270,
                    ShadowDepth = 5,
                    Opacity = 0.05,
                    Color = Colors.Black
                };
                tableShadow.Freeze();
                Set("ThemeTableShadow", tableShadow);
            }
            else
            {
                // Dark theme
                Set("ThemeBg",              B("#1a191b"));
                Set("ThemeSidebarBg",       B("#141416"));
                Set("ThemeSidebarBorder",   B("#222222"));
                Set("ThemePanelBg",         B("#1a1a1c"));
                Set("ThemeTableBg",         B("#1E1C1F"));
                Set("ThemeCardBg",          B("#2a2a2c"));
                Set("ThemeCardBorder",      B("#333333"));
                Set("ThemeInputBg",         B("#1a1a1c"));
                Set("ThemeInputBorder",     B("#333333"));
                Set("ThemeInputDisabledBg", B("#121214"));

                Set("ThemeFgPrimary",   new SolidColorBrush(Colors.White));
                Set("ThemeFgSecondary", new SolidColorBrush(Colors.Gray));
                Set("ThemeFgMuted",     B("#888888"));

                // Navigation (dark sidebar styling)
                Set("ThemeSidebarFg",      B("#888888"));
                Set("ThemeNavHoverBg",     B("#333333"));
                Set("ThemeNavHoverFg",     new SolidColorBrush(Colors.White));
                Set("ThemeSubNavActiveBg", B("#2a2a2e"));
                Set("ThemeSubNavHoverBg",  B("#252528"));

                Set("ThemeScrollThumb",        B("#3a3a3c"));
                Set("ThemeDataGridAltRow",     B("#1a1a1c"));
                Set("ThemeDataGridSelected",   B("#222222"));
                Set("ThemeDataGridSelectedFg", new SolidColorBrush(Colors.White));
                Set("ThemeBadgeBg",            B("#2a2a2c"));
                Set("ThemeBadgeFg",            new SolidColorBrush(Colors.White));
                Set("ThemeSeparator",          B("#222222"));
                Set("ThemeDropdownBg",         B("#1a1a1c"));
                Set("ThemeDropdownBorder",     B("#333333"));
                Set("ThemeDropdownHoverBg",    B("#333333"));
                Set("ThemeComboArrow",         new SolidColorBrush(Colors.White));
                Set("ThemeNavBtnBg",           B("#1a1a1c"));
                Set("ThemeNavBtnBorder",       B("#333333"));
                Set("ThemeNavBtnHoverBg",      B("#2a2a2c"));

                Set("ThemeMemberBadgeDailyBg",   B("#3a2200"));
                Set("ThemeMemberBadgeWeeklyBg",  B("#0a2a2a"));
                Set("ThemeMemberBadgeHalfBg",    B("#251a3a"));
                Set("ThemeMemberBadgeMonthlyBg", B("#0a1a3a"));
                Set("ThemeMemberBadgeYearlyBg",  B("#0a2a1a"));
                Set("ThemeMemberBadgePromoBg",   B("#3a0a1a"));

                // ── Original Rich Dark Theme Cards ──────────────────────
                Set("ThemeCheckinsBg",           B("#1a3d2b"));
                Set("ThemeCheckinsBorder",       B("#26593f"));
                Set("ThemeCheckinsFg",           new SolidColorBrush(Colors.White));
                Set("ThemeCheckinsLabelFg",      B("#888888"));
                Set("ThemeCheckinsIconFg",       B("#4caf50"));

                Set("ThemeOccupancyBg",          B("#142622"));
                Set("ThemeOccupancyBorder",      B("#22423c"));
                Set("ThemeOccupancyFg",          new SolidColorBrush(Colors.White));
                Set("ThemeOccupancyLabelFg",     B("#888888"));
                Set("ThemeOccupancyIconFg",      B("#26c6a6"));

                Set("ThemeTodayRevenueBg",       B("#143d26"));
                Set("ThemeTodayRevenueBorder",   B("#1e5938"));
                Set("ThemeTodayRevenueFg",       new SolidColorBrush(Colors.White));
                Set("ThemeTodayRevenueLabelFg",  B("#888888"));
                Set("ThemeTodayRevenueIconFg",   B("#4caf50"));

                Set("ThemeMonthlyRevenueBg",     B("#1a3520"));
                Set("ThemeMonthlyRevenueBorder", B("#254d2f"));
                Set("ThemeMonthlyRevenueFg",     new SolidColorBrush(Colors.White));
                Set("ThemeMonthlyRevenueLabelFg",B("#888888"));
                Set("ThemeMonthlyRevenueIconFg", B("#81c784"));

                Set("ThemeNewMembersBg",         B("#3d2614"));
                Set("ThemeNewMembersBorder",     B("#59391e"));
                Set("ThemeNewMembersFg",         new SolidColorBrush(Colors.White));
                Set("ThemeNewMembersLabelFg",    B("#888888"));
                Set("ThemeNewMembersIconFg",     B("#ff9800"));

                // Inline check-in card
                Set("ThemeInlineAttendanceBg",          B("#162238"));
                Set("ThemeInlineAttendanceBorder",      B("#263b5e"));
                Set("ThemeInlineAttendanceLabelFg",     B("#85a3d0"));
                Set("ThemeInlineAttendanceButtonBg",    B("#263b5e"));
                Set("ThemeInlineAttendanceButtonBorder",B("#3f5a8a"));
                Set("ThemeInlineAttendanceButtonFg",    new SolidColorBrush(Colors.White));
                Set("ThemeInlineAttendanceButtonHoverBg",B("#334e7c"));

                // Expiration report urgency rows
                Set("ThemeExpiredRowBg",                B("#1f0a0a"));
                Set("ThemeCriticalRowBg",               B("#291407"));
                Set("ThemeWarningRowBg",                B("#1a1500"));

                // Alerts Fg
                Set("ThemeAlertRedFg",           B("#EF4444"));
                Set("ThemeAlertOrangeFg",        B("#ff9800"));

                // Payment badges
                Set("ThemeBadgeCashBg",          B("#1a2b1a"));
                Set("ThemeBadgeCashFg",          B("#81c784"));
                Set("ThemeBadgeGCashBg",         B("#0d1f3d"));
                Set("ThemeBadgeGCashFg",         B("#4a90e2"));
                Set("ThemeBadgeRefundBg",        B("#3B1214"));
                Set("ThemeBadgeRefundFg",        B("#EF4444"));

                // Specializations
                Set("ThemeSpecializationBadgeBg",     B("#2b1b10"));
                Set("ThemeSpecializationBadgeFg",     B("#ffb74d"));
                Set("ThemeSpecializationBadgeBorder", B("#ff6b00"));

                // Discount / Promo badges
                Set("ThemeDiscountBadgeBg",      B("#2D1A3D"));
                Set("ThemeDiscountBadgeFg",      B("#B176F0"));

                // Role & Status Badges
                Set("ThemeRoleAdminBg",       B("#2D1A3D"));
                Set("ThemeRoleAdminFg",       B("#B176F0"));
                Set("ThemeRoleStaffBg",       B("#1A2B3D"));
                Set("ThemeRoleStaffFg",       B("#5BA4E5"));
                Set("ThemeStatusActiveBg",    B("#1A3D25"));
                Set("ThemeStatusActiveFg",    B("#5BE582"));
                Set("ThemeStatusInactiveBg",  B("#3D1A1A"));
                Set("ThemeStatusInactiveFg",  B("#E55B5B"));

                // Refunds & Audits
                Set("ThemeRefundReasonFg",    B("#fbbf24"));
                Set("ThemeAuditHeaderFg",     B("#ff6b00"));

                // Info/Note alert boxes
                Set("ThemeNoteAlertBg",       B("#1a2533"));
                Set("ThemeNoteAlertFg",       B("#88aacc"));
                Set("ThemeNoteAlertIconFg",   B("#4090f0"));

                // Shadows disabled in Dark Mode
                Set("ThemeCardShadow", null);
                Set("ThemeTableShadow", null);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────
        private static Color C(string hex)           => (Color)ColorConverter.ConvertFromString(hex);
        private static SolidColorBrush B(string hex) => new SolidColorBrush(C(hex));
        private static void Set(string key, object? value) => Application.Current.Resources[key] = value;
    }
}
