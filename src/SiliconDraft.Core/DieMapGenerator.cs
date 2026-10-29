namespace SiliconDraft.Core;

internal static class DieMapGenerator
{
    public const int MaxDiePositions = 1_000_000;

    private const double ToleranceMm = 1e-9;

    public static DieMapResult TryGenerate(WaferGeometry geometry)
    {
        var radius = geometry.DiameterMm / 2;
        var usableRadius = geometry.UsableRadiusMm;
        var halfWidth = geometry.DieWidthMm / 2;
        var halfHeight = geometry.DieHeightMm / 2;
        var halfDiagonal = Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

        var (originX, originY) = Transform(
            geometry.GridOffsetXMm,
            geometry.GridOffsetYMm,
            geometry.RotationDeg,
            geometry.MirrorX,
            geometry.MirrorY);
        var (columnStepX, columnStepY) = Transform(
            geometry.PitchWidthMm, 0, geometry.RotationDeg, geometry.MirrorX, geometry.MirrorY);
        var (rowStepX, rowStepY) = Transform(
            0, geometry.PitchHeightMm, geometry.RotationDeg, geometry.MirrorX, geometry.MirrorY);

        var limit = radius + halfDiagonal;
        var columnBounds = IndexBounds(columnStepX, columnStepY, originX, originY, limit);
        var rowBounds = IndexBounds(rowStepX, rowStepY, originX, originY, limit);
        if (columnBounds is null || rowBounds is null)
        {
            return TooManyPositions();
        }

        var columns = columnBounds.Value;
        var rows = rowBounds.Value;
        if (columns.IsEmpty || rows.IsEmpty)
        {
            return DieMapResult.Failed("geometry produces no valid die");
        }

        var candidateCount = columns.Length * rows.Length;
        if (candidateCount > MaxDiePositions)
        {
            return TooManyPositions();
        }

        var valid = new List<LatticeDie>();
        var partial = new List<LatticeDie>();
        for (var row = rows.Min; row <= rows.Max; row++)
        {
            var rowOriginX = originX + row * rowStepX;
            var rowOriginY = originY + row * rowStepY;
            for (var column = columns.Min; column <= columns.Max; column++)
            {
                var centerX = rowOriginX + column * columnStepX;
                var centerY = rowOriginY + column * columnStepY;
                if (MaxCornerDistanceSquared(centerX, centerY, halfWidth, halfHeight, geometry.RotationDeg)
                    <= Square(usableRadius + ToleranceMm))
                {
                    valid.Add(new LatticeDie(centerX, centerY));
                }
                else if (DistanceToRectangle(centerX, centerY, halfWidth, halfHeight, geometry.RotationDeg)
                    <= radius + ToleranceMm)
                {
                    partial.Add(new LatticeDie(centerX, centerY));
                }
            }
        }

        if (valid.Count == 0)
        {
            return DieMapResult.Failed("geometry produces no valid die");
        }

        var (axisX, axisY) = Transform(1, 0, geometry.RotationDeg, geometry.MirrorX, geometry.MirrorY);
        var (rowAxisX, rowAxisY) = Transform(0, 1, geometry.RotationDeg, geometry.MirrorX, geometry.MirrorY);
        var (anchorU, anchorV) = CoordinateAnchor(valid, geometry.IndexOrigin, axisX, axisY, rowAxisX, rowAxisY);

        var dies = new List<Die>(valid.Count + partial.Count);
        foreach (var die in valid)
        {
            dies.Add(MakeDie(die, DieDisposition.Valid));
        }

        var retained = 0;
        if (geometry.PartialDiePolicy == PartialDiePolicy.Retain)
        {
            foreach (var die in partial)
            {
                dies.Add(MakeDie(die, DieDisposition.Partial));
            }

            retained = partial.Count;
        }

        dies.Sort((first, second) => first.Y != second.Y
            ? first.Y.CompareTo(second.Y)
            : first.X.CompareTo(second.X));

        return DieMapResult.Succeeded(new DieMap(
            dies,
            new DieCounts(
                GrossDie: valid.Count,
                PartialDieFound: partial.Count,
                PartialDieRetained: retained)));

        Die MakeDie(LatticeDie latticeDie, DieDisposition disposition)
        {
            var u = latticeDie.X * axisX + latticeDie.Y * axisY;
            var v = latticeDie.X * rowAxisX + latticeDie.Y * rowAxisY;
            return new Die(
                RoundToIndex((u - anchorU) / geometry.PitchWidthMm),
                RoundToIndex((v - anchorV) / geometry.PitchHeightMm),
                latticeDie.X,
                latticeDie.Y,
                disposition);
        }
    }

    private static (double U, double V) CoordinateAnchor(
        List<LatticeDie> valid,
        DieIndexOrigin indexOrigin,
        double axisX,
        double axisY,
        double rowAxisX,
        double rowAxisY)
    {
        if (indexOrigin == DieIndexOrigin.Center)
        {
            var origin = valid[0];
            var bestDistance = Square(origin.X) + Square(origin.Y);
            foreach (var candidate in valid)
            {
                var distance = Square(candidate.X) + Square(candidate.Y);
                if (distance < bestDistance ||
                    (distance == bestDistance &&
                     (candidate.X < origin.X ||
                      (candidate.X == origin.X && candidate.Y < origin.Y))))
                {
                    origin = candidate;
                    bestDistance = distance;
                }
            }

            return (origin.X * axisX + origin.Y * axisY, origin.X * rowAxisX + origin.Y * rowAxisY);
        }

        var minU = double.PositiveInfinity;
        var maxU = double.NegativeInfinity;
        var minV = double.PositiveInfinity;
        var maxV = double.NegativeInfinity;
        foreach (var die in valid)
        {
            var u = die.X * axisX + die.Y * axisY;
            var v = die.X * rowAxisX + die.Y * rowAxisY;
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }

        return indexOrigin switch
        {
            DieIndexOrigin.BottomLeft => (minU, minV),
            DieIndexOrigin.TopLeft => (minU, maxV),
            DieIndexOrigin.TopRight => (maxU, maxV),
            DieIndexOrigin.BottomRight => (maxU, minV),
            _ => throw new ArgumentOutOfRangeException(nameof(indexOrigin)),
        };
    }

    /// <summary>
    /// Returns the lattice indices whose centers can fall within <paramref name="limit"/>
    /// of the wafer center, or <c>null</c> when the grid has too many candidate positions.
    /// </summary>
    private static IndexRange? IndexBounds(
        double stepX, double stepY, double originX, double originY, double limit)
    {
        var coefficient = stepX != 0 ? stepX : stepY;
        var origin = stepX != 0 ? originX : originY;
        var first = (-limit - origin) / coefficient;
        var second = (limit - origin) / coefficient;
        var low = Math.Min(first, second);
        var high = Math.Max(first, second);
        if (!double.IsFinite(low) || !double.IsFinite(high) || high - low > MaxDiePositions)
        {
            return null;
        }

        // A lattice entirely far from the wafer contributes no candidate; an empty range
        // lets the caller report "no valid die" instead of overflowing indices.
        if (low <= long.MinValue || high >= long.MaxValue)
        {
            return IndexRange.Empty;
        }

        var min = (long)Math.Floor(low) - 1;
        var max = (long)Math.Ceiling(high) + 1;
        if (min < int.MinValue || max > int.MaxValue)
        {
            return IndexRange.Empty;
        }

        return new IndexRange((int)min, (int)max);
    }

    private static DieMapResult TooManyPositions() =>
        DieMapResult.Failed($"die grid produces more than {MaxDiePositions} die positions");

    private static double MaxCornerDistanceSquared(
        double centerX, double centerY, double halfWidth, double halfHeight, int rotationDeg)
    {
        var (firstX, firstY) = Rotate(halfWidth, halfHeight, rotationDeg);
        var (secondX, secondY) = Rotate(-halfWidth, halfHeight, rotationDeg);
        var (thirdX, thirdY) = Rotate(-halfWidth, -halfHeight, rotationDeg);
        var (fourthX, fourthY) = Rotate(halfWidth, -halfHeight, rotationDeg);
        return Math.Max(
            Math.Max(
                Square(centerX + firstX) + Square(centerY + firstY),
                Square(centerX + secondX) + Square(centerY + secondY)),
            Math.Max(
                Square(centerX + thirdX) + Square(centerY + thirdY),
                Square(centerX + fourthX) + Square(centerY + fourthY)));
    }

    private static double DistanceToRectangle(
        double centerX, double centerY, double halfWidth, double halfHeight, int rotationDeg)
    {
        // Move the wafer center into the die's local (unrotated) frame.
        var (localX, localY) = Rotate(-centerX, -centerY, (360 - rotationDeg) % 360);
        var outsideX = Math.Max(Math.Abs(localX) - halfWidth, 0);
        var outsideY = Math.Max(Math.Abs(localY) - halfHeight, 0);
        return Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
    }

    private static (double X, double Y) Transform(
        double x, double y, int rotationDeg, bool mirrorX, bool mirrorY)
    {
        if (mirrorX)
        {
            x = -x;
        }

        if (mirrorY)
        {
            y = -y;
        }

        return Rotate(x, y, rotationDeg);
    }

    private static (double X, double Y) Rotate(double x, double y, int rotationDeg) => rotationDeg switch
    {
        0 => (x, y),
        90 => (-y, x),
        180 => (-x, -y),
        270 => (y, -x),
        _ => throw new ArgumentOutOfRangeException(nameof(rotationDeg)),
    };

    private static int RoundToIndex(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static double Square(double value) => value * value;

    private readonly record struct LatticeDie(double X, double Y);

    private readonly record struct IndexRange(int Min, int Max)
    {
        public static IndexRange Empty => new(0, -1);

        public bool IsEmpty => Min > Max;

        public long Length => (long)Max - Min + 1;
    }
}

internal readonly record struct DieMapResult(DieMap? Map, string? Error)
{
    public static DieMapResult Succeeded(DieMap map) => new(map, null);

    public static DieMapResult Failed(string error) => new(null, error);
}
