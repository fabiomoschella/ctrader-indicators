using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(AccessRights = AccessRights.None,  IsOverlay = true)]
    public class PivotPointsStandard : Indicator
    {
        [Output("Pivot", LineColor = "White")]
        public IndicatorDataSeries Pivot { get; set; }

        [Output("R1", LineColor = "Green")]
        public IndicatorDataSeries R1 { get; set; }

        [Output("R2", LineColor = "Green")]
        public IndicatorDataSeries R2 { get; set; }

        [Output("R3", LineColor = "Green")]
        public IndicatorDataSeries R3 { get; set; }

        [Output("S1", LineColor = "Red")]
        public IndicatorDataSeries S1 { get; set; }

        [Output("S2", LineColor = "Red")]
        public IndicatorDataSeries S2 { get; set; }

        [Output("S3", LineColor = "Red")]
        public IndicatorDataSeries S3 { get; set; }

        Bars _bars;

        protected override void Initialize()
        {
            _bars = MarketData.GetBars(TimeFrame.Daily, SymbolName);
        }

        public override void Calculate(int index)
        {
            var pivot = (_bars.HighPrices.Last(1) + _bars.LowPrices.Last(1) + _bars.ClosePrices.Last(1)) / 3;

            var r1 = 2 * pivot - _bars.LowPrices.Last(1);
            var s1 = 2 * pivot - _bars.HighPrices.Last(1);

            var r2 = pivot + _bars.HighPrices.Last(1) - _bars.LowPrices.Last(1);
            var s2 = pivot - _bars.HighPrices.Last(1) + _bars.LowPrices.Last(1);

            var r3 = _bars.HighPrices.Last(1) + 2 * (pivot - _bars.LowPrices.Last(1));
            var s3 = _bars.LowPrices.Last(1) - 2 * (_bars.HighPrices.Last(1) - pivot);

            Pivot[index] = pivot;
            R1[index] = r1;
            R2[index] = r2;
            R3[index] = r3;
            S1[index] = s1;
            S2[index] = s2;
            S3[index] = s3;

            Chart.DrawTrendLine("pivot ", _bars.OpenTimes.LastValue, pivot, _bars.OpenTimes.LastValue.AddDays(1), pivot, Color.White);
            Chart.DrawTrendLine("r1 ", _bars.OpenTimes.LastValue, r1, _bars.OpenTimes.LastValue.AddDays(1), r1, Color.Green);
            Chart.DrawTrendLine("r2 ", _bars.OpenTimes.LastValue, r2, _bars.OpenTimes.LastValue.AddDays(1), r2, Color.Green);
            Chart.DrawTrendLine("r3 ", _bars.OpenTimes.LastValue, r3, _bars.OpenTimes.LastValue.AddDays(1), r3, Color.Green);
            Chart.DrawTrendLine("s1 ", _bars.OpenTimes.LastValue, s1, _bars.OpenTimes.LastValue.AddDays(1), s1, Color.Red);
            Chart.DrawTrendLine("s2 ", _bars.OpenTimes.LastValue, s2, _bars.OpenTimes.LastValue.AddDays(1), s2, Color.Red);
            Chart.DrawTrendLine("s3 ", _bars.OpenTimes.LastValue, s3, _bars.OpenTimes.LastValue.AddDays(1), s3, Color.Red);

        }
    }
}