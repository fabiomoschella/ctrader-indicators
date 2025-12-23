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

        private TypicalPrice _typicalPrice;

        // Caching variables for incremental calculation
        private DateTime _currentDay;
        private double _cumulativeTPV;    // Cumulative Typical Price × Volume
        private double _cumulativeVolume;
        private int _dayStartIndex;

        // Store daily prices for std dev calculation (much lighter than recalculating)
        private System.Collections.Generic.List<double> _dailyPrices;

        // ========== INITIALIZE ==========
        
        protected override void Initialize()
        {
            // Initialize typical price indicator
            _typicalPrice = Indicators.TypicalPrice();

            // Initialize caching variables
            _currentDay = DateTime.MinValue;
            _cumulativeTPV = 0;
            _cumulativeVolume = 0;
            _dayStartIndex = 0;
            _dailyPrices = new System.Collections.Generic.List<double>();

            Print("VWAPStdDevBands initialized successfully");
            Print("Using optimized incremental VWAP calculation with daily reset");
        }
        
        // ========== CALCULATE ==========
        
        public override void Calculate(int index)
        {
            // Skip if not enough data
            if (index < 1)
                return;

            DateTime barDay = Bars.OpenTimes[index].Date;

            // Detect new day and reset cumulative values
            if (barDay != _currentDay)
            {
                _currentDay = barDay;
                _cumulativeTPV = 0;
                _cumulativeVolume = 0;
                _dayStartIndex = index;
                _dailyPrices.Clear();
            }

            // Calculate typical price and volume for current bar
            double typicalPrice = (Bars.HighPrices[index] + Bars.LowPrices[index] + Bars.ClosePrices[index]) / 3;
            double volume = Bars.TickVolumes[index];

            // Update cumulative values incrementally (O(1) operation)
            _cumulativeTPV += typicalPrice * volume;
            _cumulativeVolume += volume;

            // Store price for std dev calculation
            double priceForStdDev = GetPriceValue(index);
            _dailyPrices.Add(priceForStdDev);

            // Calculate VWAP from cached cumulative values
            if (_cumulativeVolume == 0)
            {
                SetNaNValues(index);
                return;
            }

            double vwap = _cumulativeTPV / _cumulativeVolume;

            // Set VWAP value
            if (ShowVWAP)
                VWAP[index] = vwap;
            else
                VWAP[index] = double.NaN;

            // Calculate standard deviation using only daily prices list (much faster)
            double stdDev = CalculateStandardDeviationOptimized(vwap);

            // Calculate and set band values
            if (ShowS1)
            {
                S1Upper[index] = vwap + (S1Multiplier * stdDev);
                S1Lower[index] = vwap - (S1Multiplier * stdDev);
            }
            else
            {
                S1Upper[index] = double.NaN;
                S1Lower[index] = double.NaN;
            }

            if (ShowS2)
            {
                S2Upper[index] = vwap + (S2Multiplier * stdDev);
                S2Lower[index] = vwap - (S2Multiplier * stdDev);
            }
            else
            {
                S2Upper[index] = double.NaN;
                S2Lower[index] = double.NaN;
            }

            if (ShowS3)
            {
                S3Upper[index] = vwap + (S3Multiplier * stdDev);
                S3Lower[index] = vwap - (S3Multiplier * stdDev);
            }
            else
            {
                S3Upper[index] = double.NaN;
                S3Lower[index] = double.NaN;
            }
        }
        
        // ========== HELPER METHODS ==========
        
        private double GetPriceValue(int index)
        {
            switch (PriceSource)
            {
                case PriceSourceType.TypicalPrice:
                    return _typicalPrice.Result[index];
                case PriceSourceType.Close:
                    return Bars.ClosePrices[index];
                case PriceSourceType.Open:
                    return Bars.OpenPrices[index];
                case PriceSourceType.High:
                    return Bars.HighPrices[index];
                case PriceSourceType.Low:
                    return Bars.LowPrices[index];
                case PriceSourceType.Median:
                    return (Bars.HighPrices[index] + Bars.LowPrices[index]) / 2;
                case PriceSourceType.Weighted:
                    return (Bars.HighPrices[index] + Bars.LowPrices[index] + 2 * Bars.ClosePrices[index]) / 4;
                default:
                    return _typicalPrice.Result[index];
            }
        }

        private double CalculateStandardDeviationOptimized(double vwap)
        {
            // Optimized: iterate only the daily prices list instead of scanning all historical bars
            // This reduces complexity from O(n²) to O(n)

            int n = _dailyPrices.Count;
            if (n <= 0)
                return 0;

            double sumSquaredDiff = 0;

            // Only iterate through prices collected today (typically hundreds, not thousands)
            for (int i = 0; i < n; i++)
            {
                double diff = _dailyPrices[i] - vwap;
                sumSquaredDiff += diff * diff;
            }

            double variance = sumSquaredDiff / n;
            return Math.Sqrt(variance);
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
