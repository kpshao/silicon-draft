using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>
/// Effective physical wafer and Die Grid geometry after defaults are applied.
/// Distances are in millimeters and rotation is counter-clockwise in degrees.
/// </summary>
public sealed record WaferGeometry
{
    internal WaferGeometry(
        double diameterMm,
        double edgeExclusionMm,
        double notchAngleDeg,
        double dieWidthMm,
        double dieHeightMm,
        double scribeMm,
        double pitchWidthMm,
        double pitchHeightMm,
        double gridOffsetXMm,
        double gridOffsetYMm,
        int rotationDeg,
        bool mirrorX,
        bool mirrorY,
        DieIndexOrigin indexOrigin,
        PartialDiePolicy partialDiePolicy)
    {
        DiameterMm = diameterMm;
        EdgeExclusionMm = edgeExclusionMm;
        NotchAngleDeg = notchAngleDeg;
        DieWidthMm = dieWidthMm;
        DieHeightMm = dieHeightMm;
        ScribeMm = scribeMm;
        PitchWidthMm = pitchWidthMm;
        PitchHeightMm = pitchHeightMm;
        GridOffsetXMm = gridOffsetXMm;
        GridOffsetYMm = gridOffsetYMm;
        RotationDeg = rotationDeg;
        MirrorX = mirrorX;
        MirrorY = mirrorY;
        IndexOrigin = indexOrigin;
        PartialDiePolicy = partialDiePolicy;
    }

    public double DiameterMm { get; }

    public double EdgeExclusionMm { get; }

    public double NotchAngleDeg { get; }

    public double DieWidthMm { get; }

    public double DieHeightMm { get; }

    public double ScribeMm { get; }

    /// <summary>Center-to-center die pitch along the grid's column axis.</summary>
    public double PitchWidthMm { get; }

    /// <summary>Center-to-center die pitch along the grid's row axis.</summary>
    public double PitchHeightMm { get; }

    public double GridOffsetXMm { get; }

    public double GridOffsetYMm { get; }

    public int RotationDeg { get; }

    public bool MirrorX { get; }

    public bool MirrorY { get; }

    public DieIndexOrigin IndexOrigin { get; }

    public PartialDiePolicy PartialDiePolicy { get; }

    /// <summary>Radius of the region in which a die must fit completely to be valid.</summary>
    [JsonIgnore]
    public double UsableRadiusMm => DiameterMm / 2 - EdgeExclusionMm;
}

public enum DieIndexOrigin
{
    Center,
    BottomLeft,
    TopLeft,
    TopRight,
    BottomRight,
}

public enum PartialDiePolicy
{
    Exclude,
    Retain,
}
