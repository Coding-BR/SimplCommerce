using MudBlazor;

namespace BlazorClient.Core.Theme;

public static class YnexTheme
{
    public static MudTheme DefaultTheme => new MudTheme()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#176B4D",
            PrimaryDarken = "#0F4D38",
            PrimaryLighten = "#DDEEE6",
            Secondary = "#B7792B",
            Success = "#16845B",
            Info = "#356C9D",
            Warning = "#B7792B",
            Error = "#C4453C",
            AppbarBackground = "#ffffff",
            AppbarText = "#17251F",
            Background = "#F5F7F4",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#33443C",
            DrawerIcon = "#607068",
            ActionDefault = "#52635B",
            TextPrimary = "#17251F",
            TextSecondary = "#617068",
            Divider = "#DDE4DF",
            TableHover = "#F2F7F4"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#4ADE80", // Vibrant Green (High visibility on dark)
            Secondary = "#38bdf8", // Sky 400
            Success = "#34d399", // Emerald 400
            Info = "#60a5fa", // Blue 400
            Warning = "#fbbf24", // Amber 400
            Error = "#f87171", // Red 400
            Background = "#0f172a", // Slate 900
            Surface = "#1e293b", // Slate 800
            DrawerBackground = "#1e293b",
            DrawerText = "#cbd5e1", // Slate 300
            DrawerIcon = "#94a3b8", // Slate 400
            AppbarBackground = "#0f172a",
            AppbarText = "#f8fafc",
            TextPrimary = "#f8fafc",
            TextSecondary = "#94a3b8",
            ActionDefault = "#94a3b8",
            Divider = "rgba(148, 163, 184, 0.1)"
        },
        Typography = new Typography()
        {
            Default = new Default()
            {
                FontFamily = new[] { "Manrope", "Segoe UI", "sans-serif" },
                FontSize = "0.875rem",
                FontWeight = 400,
                LineHeight = 1.5,
                LetterSpacing = "0.01071em"
            },
            H1 = new H1() { FontSize = "clamp(2.15rem, 5vw, 4.25rem)", FontWeight = 800, LineHeight = 1.05, LetterSpacing = "-0.04em" },
            H2 = new H2() { FontSize = "clamp(1.85rem, 3.6vw, 3rem)", FontWeight = 800, LineHeight = 1.1, LetterSpacing = "-0.035em" },
            H3 = new H3() { FontSize = "1.75rem", FontWeight = 700, LineHeight = 1.2 },
            H4 = new H4() { FontSize = "1.5rem", FontWeight = 700, LineHeight = 1.2 },
            H5 = new H5() { FontSize = "1.25rem", FontWeight = 700, LineHeight = 1.2 },
            H6 = new H6() { FontSize = "1rem", FontWeight = 600, LineHeight = 1.2 },
            Button = new Button() { FontSize = "0.875rem", FontWeight = 700, TextTransform = "none" },
            Body1 = new Body1() { FontSize = "0.875rem", FontWeight = 400 },
            Body2 = new Body2() { FontSize = "0.8125rem", FontWeight = 400 },
            Caption = new Caption() { FontSize = "0.75rem", FontWeight = 400 },
            Overline = new Overline() { FontSize = "0.6875rem", FontWeight = 700, TextTransform = "uppercase", LetterSpacing = "1px" }
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "14px",
            AppbarHeight = "76px",
            DrawerWidthLeft = "276px"
        }
    };
}
