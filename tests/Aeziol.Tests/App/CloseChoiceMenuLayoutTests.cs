using System.Globalization;
using System.Xml.Linq;

namespace Aeziol.Tests.App;

public sealed class CloseChoiceMenuLayoutTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void RememberSection_IsClearlySeparatedWhileStayingCompactAndAccessible()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        var rememberItem = FindNamedElement(document, "CloseRememberMenuItem");
        var rememberContent = FindNamedElement(document, "CloseRememberContent");
        var precedingSeparator = FindNamedElement(document, "CloseRememberSeparator");
        var contextMenu = rememberItem.Ancestors().Single(element => element.Name.LocalName == "ContextMenu");
        var menuItemStyle = contextMenu
            .Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && (string?)element.Attribute("TargetType") == "MenuItem");
        var actionPadding = ParseThickness(menuItemStyle
            .Elements()
            .Single(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "Padding")
            .Attribute("Value")?.Value);
        var rememberPadding = ParseThickness(rememberItem.Attribute("Padding")?.Value);
        var separatorMargin = ParseThickness(precedingSeparator.Attribute("Margin")?.Value);

        var actionHeight = 14 + actionPadding.Top + actionPadding.Bottom;
        var rememberItemHeight = ParseDouble(rememberContent.Attribute("Height")?.Value)
            + rememberPadding.Top
            + rememberPadding.Bottom;
        var separatorFootprint = ParseDouble(precedingSeparator.Attribute("Height")?.Value)
            + separatorMargin.Top
            + separatorMargin.Bottom;
        var rememberSectionHeight = rememberItemHeight + separatorFootprint;

        Assert.Equal(actionHeight, rememberItemHeight);
        Assert.InRange(separatorFootprint, 9, 12);
        Assert.InRange(separatorMargin.Top, 4, 6);
        Assert.InRange(separatorMargin.Bottom, 4, 6);
        Assert.True(rememberSectionHeight <= actionHeight + 12,
            $"Remember section is {rememberSectionHeight}px high but the compact budget is {actionHeight + 12}px.");
        Assert.Equal("{DynamicResource AeziolMuted}", precedingSeparator.Attribute("Background")?.Value);
        Assert.InRange(ParseDouble(precedingSeparator.Attribute("Opacity")?.Value), 0.85, 1);
        Assert.Contains(precedingSeparator.Descendants(), element =>
            element.Name.LocalName == "Border"
            && (string?)element.Attribute("Background") == "{TemplateBinding Background}");
        Assert.Equal("True", rememberItem.Attribute("IsCheckable")?.Value);
        Assert.Equal("True", rememberItem.Attribute("StaysOpenOnClick")?.Value);
        Assert.Equal("{TemplateBinding Padding}", menuItemStyle
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XamlNamespace + "Name") == "MenuChrome")
            .Attribute("Padding")?.Value);

        var note = FindNamedElement(document, "CloseRememberMenuNoteText");
        Assert.Equal("NoWrap", note.Attribute("TextWrapping")?.Value);
        Assert.Equal("CharacterEllipsis", note.Attribute("TextTrimming")?.Value);

        var columns = rememberContent
            .Descendants()
            .Where(element => element.Name.LocalName == "ColumnDefinition")
            .ToArray();
        Assert.Equal("23", columns[0].Attribute("Width")?.Value);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(XamlNamespace + "Name") == name);

    private static double ParseDouble(string? value) =>
        double.Parse(Assert.IsType<string>(value), CultureInfo.InvariantCulture);

    private static ThicknessValues ParseThickness(string? value)
    {
        var parts = Assert.IsType<string>(value)
            .Split(',')
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        return parts.Length switch
        {
            1 => new ThicknessValues(parts[0], parts[0], parts[0], parts[0]),
            2 => new ThicknessValues(parts[0], parts[1], parts[0], parts[1]),
            4 => new ThicknessValues(parts[0], parts[1], parts[2], parts[3]),
            _ => throw new InvalidOperationException("Unsupported XAML thickness."),
        };
    }

    private sealed record ThicknessValues(double Left, double Top, double Right, double Bottom);
}
