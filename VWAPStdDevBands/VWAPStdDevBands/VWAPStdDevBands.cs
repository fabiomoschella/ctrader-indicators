using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class VWAPStdDevBands : Indicator
    {
        // ========== PARAMETERS ==========

        // Standard Deviation multipliers
        [Parameter("S1 Multiplier", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1)]
        public double S1Multiplier { get; set; }

        [Parameter("S2 Multiplier", DefaultValue = 2.0, MinValue = 0.1, Step = 0.1)]
        public double S2Multiplier { get; set; }

        [Parameter("S3 Multiplier", DefaultValue = 3.0, MinValue = 0.1, Step = 0.1)]
        public double S3Multiplier { get; set; }

        // Show/Hide bands
        [Parameter("Show VWAP", DefaultValue = true)]
        public bool ShowVWAP { get; set; }

        [Parameter("Show S1 Bands", DefaultValue = false)]
        public bool ShowS1 { get; set; }

        [Parameter("Show S2 Bands", DefaultValue = false)]
        public bool ShowS2 { get; set; }

        [Parameter("Show S3 Bands", DefaultValue = false)]
        public bool ShowS3 { get; set; }

        // Price source for deviation calculation
        [Parameter("Price Source", DefaultValue = PriceSourceType.Close)]
        public PriceSourceType PriceSource { get; set; }

        // ========== OUTPUTS ==========

        [Output("VWAP", LineColor = "White", PlotType = PlotType.Line, Thickness = 2)]
        public IndicatorDataSeries VWAP { get; set; }

        [Output("S1 Upper", LineColor = "DeepSkyBlue", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S1Upper { get; set; }

        [Output("S1 Lower", LineColor = "DeepSkyBlue", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S1Lower { get; set; }

        [Output("S2 Upper", LineColor = "Orange", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S2Upper { get; set; }

        [Output("S2 Lower", LineColor = "Orange", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S2Lower { get; set; }

        [Output("S3 Upper", LineColor = "Red", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S3Upper { get; set; }

        [Output("S3 Lower", LineColor = "Red", PlotType = PlotType.Line, Thickness = 1)]
        public IndicatorDataSeries S3Lower { get; set; }

        // ========== PRIVATE VARIABLES ==========

        // Incremental VWAP state
        private DateTime _currentDay;
        private double _cumulativeTPV;      // Cumulative (TypicalPrice × Volume)
        private double _cumulativeVolume;
        private int _dayStartIndex;

        // Incremental Std Dev state (avoids re-iterating all daily bars)
        private double _cumulativeSumSqDiff; // Σ (price - vwap_at_that_time)² — recalculated on day boundary
        private int _dayBarCount;            // Number of bars accumulated today

        // Guards against redundant recalculation on same tick
        private int _lastCalculatedIndex = -1;

        // ========== INITIALIZE ==========

        protected override void Initialize()
        {
            _currentDay = DateTime.MinValue;
            _cumulativeTPV = 0;
            _cumulativeVolume = 0;
            _dayStartIndex = 0;
            _cumulativeSumSqDiff = 0;
            _dayBarCount = 0;
            _lastCalculatedIndex = -1;
        }

        // ========== CALCULATE ==========

        public override void Calculate(int index)
        {
            if (index < 1)
                return;

            DateTime barDay = Bars.OpenTimes[index].Date;

            // --- Handle out-of-sequence access (scrolling back in history) ---
            // If cTrader requests a bar earlier than we expect, do a full recalc for that day.
            if (index < _dayStartIndex || barDay < _currentDay)
            {
                FullRecalculate(index);
                return;
            }

            // --- Same tick update: the last bar is being refreshed (price changed) ---
            // Recalculate only the current bar from scratch within the day.
            if (index == _lastCalculatedIndex && barDay == _currentDay)
            {
                RecalculateCurrentBar(index);
                return;
            }

            // --- New day detected: reset all accumulators ---
            if (barDay != _currentDay)
            {
                _currentDay = barDay;
                _cumulativeTPV = 0;
                _cumulativeVolume = 0;
                _dayStartIndex = index;
                _cumulativeSumSqDiff = 0;
                _dayBarCount = 0;
            }

            // --- Normal incremental path (new bar in sequence) ---
            double typicalPrice = (Bars.HighPrices[index] + Bars.LowPrices[index] + Bars.ClosePrices[index]) / 3.0;
            double volume = Bars.TickVolumes[index];

            _cumulativeTPV += typicalPrice * volume;
            _cumulativeVolume += volume;
            _dayBarCount++;

            if (_cumulativeVolume == 0)
            {
                SetNaNValues(index);
                _lastCalculatedIndex = index;
                return;
            }

            double vwap = _cumulativeTPV / _cumulativeVolume;

            // Incremental sum-of-squared-differences for std dev
            double price = GetPriceValue(index);
            double diff = price - vwap;
            _cumulativeSumSqDiff += diff * diff;

            SetOutputValues(index, vwap, _cumulativeSumSqDiff, _dayBarCount);
            _lastCalculatedIndex = index;
        }

        // ========== RECALCULATION METHODS ==========

        /// <summary>
        /// Recalculates the last bar of the current day when the same index is
        /// requested again (tick update). Avoids a full day scan by subtracting
        /// the previous contribution and adding the updated one.
        /// </summary>
        private void RecalculateCurrentBar(int index)
        {
            // Full recalculate from day start is the safest approach for a same-bar update
            // because volume and price may have changed, affecting VWAP and therefore all stddev.
            // But we limit the recalc to only today's bars (small set).
            _cumulativeTPV = 0;
            _cumulativeVolume = 0;
            _cumulativeSumSqDiff = 0;
            _dayBarCount = 0;

            // First pass: compute VWAP up to current index
            for (int i = _dayStartIndex; i <= index; i++)
            {
                double tp = (Bars.HighPrices[i] + Bars.LowPrices[i] + Bars.ClosePrices[i]) / 3.0;
                double vol = Bars.TickVolumes[i];
                _cumulativeTPV += tp * vol;
                _cumulativeVolume += vol;
                _dayBarCount++;
            }

            if (_cumulativeVolume == 0)
            {
                SetNaNValues(index);
                return;
            }

            double vwap = _cumulativeTPV / _cumulativeVolume;

            // Second pass: compute std dev against final VWAP
            for (int i = _dayStartIndex; i <= index; i++)
            {
                double price = GetPriceValue(i);
                double d = price - vwap;
                _cumulativeSumSqDiff += d * d;
            }

            SetOutputValues(index, vwap, _cumulativeSumSqDiff, _dayBarCount);
        }

        /// <summary>
        /// Full recalculation from the beginning of the day containing <paramref name="index"/>.
        /// Used when bars are requested out of sequence (e.g., scrolling back in history).
        /// </summary>
        private void FullRecalculate(int index)
        {
            DateTime targetDay = Bars.OpenTimes[index].Date;

            // Find the first bar of this day
            int start = index;
            while (start > 0 && Bars.OpenTimes[start - 1].Date == targetDay)
                start--;

            _currentDay = targetDay;
            _dayStartIndex = start;
            _cumulativeTPV = 0;
            _cumulativeVolume = 0;
            _cumulativeSumSqDiff = 0;
            _dayBarCount = 0;

            // First pass: compute VWAP
            for (int i = start; i <= index; i++)
            {
                double tp = (Bars.HighPrices[i] + Bars.LowPrices[i] + Bars.ClosePrices[i]) / 3.0;
                double vol = Bars.TickVolumes[i];
                _cumulativeTPV += tp * vol;
                _cumulativeVolume += vol;
                _dayBarCount++;
            }

            if (_cumulativeVolume == 0)
            {
                SetNaNValues(index);
                _lastCalculatedIndex = index;
                return;
            }

            double vwap = _cumulativeTPV / _cumulativeVolume;

            // Second pass: std dev
            for (int i = start; i <= index; i++)
            {
                double price = GetPriceValue(i);
                double d = price - vwap;
                _cumulativeSumSqDiff += d * d;
            }

            SetOutputValues(index, vwap, _cumulativeSumSqDiff, _dayBarCount);
            _lastCalculatedIndex = index;
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Sets all output series values for the given index.
        /// </summary>
        private void SetOutputValues(int index, double vwap, double sumSqDiff, int count)
        {
            VWAP[index] = ShowVWAP ? vwap : double.NaN;

            double stdDev = count > 0 ? Math.Sqrt(sumSqDiff / count) : 0;

            if (ShowS1)
            {
                S1Upper[index] = vwap + S1Multiplier * stdDev;
                S1Lower[index] = vwap - S1Multiplier * stdDev;
            }
            else
            {
                S1Upper[index] = double.NaN;
                S1Lower[index] = double.NaN;
            }

            if (ShowS2)
            {
                S2Upper[index] = vwap + S2Multiplier * stdDev;
                S2Lower[index] = vwap - S2Multiplier * stdDev;
            }
            else
            {
                S2Upper[index] = double.NaN;
                S2Lower[index] = double.NaN;
            }

            if (ShowS3)
            {
                S3Upper[index] = vwap + S3Multiplier * stdDev;
                S3Lower[index] = vwap - S3Multiplier * stdDev;
            }
            else
            {
                S3Upper[index] = double.NaN;
                S3Lower[index] = double.NaN;
            }
        }

        private double GetPriceValue(int index)
        {
            switch (PriceSource)
            {
                case PriceSourceType.Close:
                    return Bars.ClosePrices[index];
                case PriceSourceType.Open:
                    return Bars.OpenPrices[index];
                case PriceSourceType.High:
                    return Bars.HighPrices[index];
                case PriceSourceType.Low:
                    return Bars.LowPrices[index];
                case PriceSourceType.Median:
                    return (Bars.HighPrices[index] + Bars.LowPrices[index]) / 2.0;
                case PriceSourceType.Weighted:
                    return (Bars.HighPrices[index] + Bars.LowPrices[index] + 2.0 * Bars.ClosePrices[index]) / 4.0;
                case PriceSourceType.TypicalPrice:
                default:
                    return (Bars.HighPrices[index] + Bars.LowPrices[index] + Bars.ClosePrices[index]) / 3.0;
            }
        }

        private void SetNaNValues(int index)
        {
            VWAP[index] = double.NaN;
            S1Upper[index] = double.NaN;
            S1Lower[index] = double.NaN;
            S2Upper[index] = double.NaN;
            S2Lower[index] = double.NaN;
            S3Upper[index] = double.NaN;
            S3Lower[index] = double.NaN;
        }
    }

    // ========== ENUMS ==========

    public enum PriceSourceType
    {
        TypicalPrice,
        Close,
        Open,
        High,
        Low,
        Median,
        Weighted
    }
}

