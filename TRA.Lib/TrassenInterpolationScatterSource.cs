using System;
using System.Collections;
using System.Collections.Generic;
using ScottPlot;

namespace TRA_Lib
{
#if USE_SCOTTPLOT
    public class TrassenInterpolationScatterSource : IScatterSource
    {
        public enum Mode { YX, XY, XS, YS, XH, YH }

        readonly Func<Interpolation> getInterpolation;
        public Mode ScatterMode { get; set; }
        public int MinRenderIndex { get; set; } = 0;
        public int MaxRenderIndex { get; set; } = int.MaxValue;

        public TrassenInterpolationScatterSource(Func<Interpolation> getter, Mode mode = Mode.YX)
        {
            getInterpolation = getter ?? throw new ArgumentNullException(nameof(getter));
            ScatterMode = mode;
        }

        IReadOnlyList<Coordinates> BuildPoints()
        {
            var interp = getInterpolation();
            int n = ScatterMode switch
            {
                Mode.XS => Math.Min(interp.X?.Length ?? 0, interp.S?.Length ?? 0),
                Mode.XH => Math.Min(interp.X?.Length ?? 0, interp.H?.Length ?? 0),
                Mode.YS => Math.Min(interp.Y?.Length ?? 0, interp.S?.Length ?? 0),
                Mode.YH => Math.Min(interp.Y?.Length ?? 0, interp.H?.Length ?? 0),
                _ => Math.Min(interp.X?.Length ?? 0, interp.Y?.Length ?? 0)
            };
            if (n == 0) return Array.Empty<Coordinates>();
            var pts = new Coordinates[n];
            for (int i = 0; i < n; i++)
            {
                double px = double.NaN, py = double.NaN;
                switch (ScatterMode)
                {
                    case Mode.YX: px = interp.Y[i]; py = interp.X[i]; break;
                    case Mode.XY: px = interp.X[i]; py = interp.Y[i]; break;
                    case Mode.XS: px = interp.X[i]; py = interp.S[i]; break;
                    case Mode.YS: px = interp.Y[i]; py = interp.S[i]; break;
                    case Mode.XH: px = interp.X[i]; py = interp.H[i]; break;
                    case Mode.YH: px = interp.Y[i]; py = interp.H[i]; break;
                }
                pts[i] = new Coordinates(px, py);
            }
            int min = Math.Max(0, MinRenderIndex);
            int max = Math.Min(pts.Length - 1, MaxRenderIndex);
            if (min == 0 && max == pts.Length - 1) return pts;
            int len = Math.Max(0, max - min + 1);
            if (len == 0) return Array.Empty<Coordinates>();
            var slice = new Coordinates[len];
            Array.Copy(pts, min, slice, 0, len);
            return slice;
        }

        public IReadOnlyList<Coordinates> GetScatterPoints() => BuildPoints();

        public DataPoint GetNearest(Coordinates location, RenderDetails renderInfo, float maxDistance = 15f)
        {
            var pts = BuildPoints();
            if (pts.Count == 0) return DataPoint.None;
            int best = -1; double bd = double.PositiveInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                double dx = pts[i].X - location.X; double dy = pts[i].Y - location.Y;
                double d2 = dx * dx + dy * dy;
                if (d2 < bd) { bd = d2; best = i; }
            }
            if (best < 0) return DataPoint.None;
            if (Math.Sqrt(bd) > Math.Max(1e-6, maxDistance)) return DataPoint.None;
            var p = pts[best];
            return new DataPoint(p.X, p.Y, best + MinRenderIndex);
        }

        public DataPoint GetNearestX(Coordinates location, RenderDetails renderInfo, float maxDistance = 15f)
        {
            var pts = BuildPoints();
            if (pts.Count == 0) return DataPoint.None;
            int best = -1; double bd = double.PositiveInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                double dx = Math.Abs(pts[i].X - location.X);
                if (dx < bd) { bd = dx; best = i; }
            }
            if (best < 0 || bd > Math.Max(1e-6, maxDistance)) return DataPoint.None;
            var p = pts[best];
            return new DataPoint(p.X, p.Y, best + MinRenderIndex);
        }

        public CoordinateRange GetLimitsX()
        {
            var pts = BuildPoints();
            if (pts.Count == 0) return new CoordinateRange(0, 0);
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (var p in pts) { if (!double.IsNaN(p.X)) { min = Math.Min(min, p.X); max = Math.Max(max, p.X); } }
            if (double.IsInfinity(min)) return new CoordinateRange(0, 0);
            return new CoordinateRange(min, max);
        }

        public CoordinateRange GetLimitsY()
        {
            var pts = BuildPoints();
            if (pts.Count == 0) return new CoordinateRange(0, 0);
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (var p in pts) { if (!double.IsNaN(p.Y)) { min = Math.Min(min, p.Y); max = Math.Max(max, p.Y); } }
            if (double.IsInfinity(min)) return new CoordinateRange(0, 0);
            return new CoordinateRange(min, max);
        }

        public AxisLimits GetLimits() => new AxisLimits(GetLimitsX().Min, GetLimitsX().Max, GetLimitsY().Min, GetLimitsY().Max);
    }
#endif
}