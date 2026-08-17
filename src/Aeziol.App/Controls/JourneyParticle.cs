namespace Aeziol.App.Controls;

public enum JourneyParticleTone
{
    Primary,
    Secondary,
    BlendedPrimary,
    BlendedSecondary,
}

public sealed class JourneyParticle
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Size { get; set; } = 2;

    public double Opacity { get; set; } = 0.45;

    public JourneyParticleTone Tone { get; set; }

    public double HighlightSize { get; set; } = 3;

    public double HighlightOpacity { get; set; } = 0.9;
}
