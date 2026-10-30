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

    /// <summary>
    /// Maps a direction in Die Index space to physical wafer space using the configured
    /// mirroring, then rotation. WCR's POS_X/POS_Y use this to describe map orientation.
    /// </summary>
    internal (double X, double Y) PhysicalDirection(double indexX, double indexY)
    {
        var x = MirrorX ? -indexX : indexX;
        var y = MirrorY ? -indexY : indexY;
        return RotationDeg switch
        {
            0 => (x, y),
            90 => (-y, x),
            180 => (-x, -y),
            270 => (y, -x),
            _ => throw new ArgumentOutOfRangeException(nameof(RotationDeg)),
        };
    }
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
