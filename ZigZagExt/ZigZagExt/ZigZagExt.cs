/*  CTRADER GURU --> Template 1.0.6

    Homepage    : https://ctrader.guru/
    Telegram    : https://t.me/ctraderguru
    Twitter     : https://twitter.com/cTraderGURU/
    Facebook    : https://www.facebook.com/ctrader.guru/
    YouTube     : https://www.youtube.com/channel/UCKkgbw09Fifj65W5t5lHeCQ
    GitHub      : https://github.com/cTraderGURU/

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace ZigZagExt
{
    public enum ModeZigZag
    {

        HighLow,
        OpenClose

    }

    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class ZigZagExt : Indicator
    {

      

        #region Identity

        public const string NAME = "ZigZag";

        public const string VERSION = "1.0.7"; // no-repaint variant: painted points are never erased

        #endregion

        #region Params

       
       
        [Parameter("Mode", DefaultValue = ModeZigZag.HighLow, Group = "Params")]
        public ModeZigZag MyModeZigZag { get; set; }

        [Parameter(DefaultValue = 12, Group = "Params")]
        public int Depth { get; set; }

        [Parameter(DefaultValue = 5, Group = "Params")]
        public int Deviation { get; set; }

        [Parameter(DefaultValue = 3, Group = "Params")]
        public int BackStep { get; set; }

        [Parameter("Source TimeFrame", Group = "Params")]
        public TimeFrame SourceTimeFrame { get; set; }

        [Parameter("Show", DefaultValue = true, Group = "Label")]
        public bool ShowLabel { get; set; }

        [Parameter("Color High", DefaultValue = "DodgerBlue", Group = "Label")]
        public Color ColorHigh { get; set; }

        [Parameter("Color Low", DefaultValue = "Red", Group = "Label")]
        public Color ColorLow { get; set; }

        [Output("ZigZag", LineColor = "DodgerBlue", LineStyle = LineStyle.Lines)]
        public IndicatorDataSeries Result { get; set; }

        #endregion

        #region Property

        private double _lastLow;
        private double _lastHigh;
        private double _low;
        private double _high;
        private int _lastHighIndex;
        private int _lastLowIndex;
        private int _type;
        private double _point;
        private double _currentLow;
        private double _currentHigh;

        private int _countHigh = 0;
        private int _countLow = 0;
        private int _waiting = 0;

        private IndicatorDataSeries _highZigZags;
        private IndicatorDataSeries _lowZigZags;

        // ===== Multi-Timeframe Source Bar State =====
        private Bars _sourceBars;
        private bool _useSourceBars;          // true when SourceTimeFrame != chart TF
        private int _lastProcessedSourceIdx = -1;

        // Source ZigZag state (mirrors chart state but for source bars)
        private double[] _srcHighZZ;
        private double[] _srcLowZZ;
        private double _srcLastLow;
        private double _srcLastHigh;
        private double _srcLow;
        private double _srcHigh;
        private int _srcLastHighIndex;
        private int _srcLastLowIndex;
        private int _srcType;
        private double _srcCurrentLow;
        private double _srcCurrentHigh;
        private int _srcCountHigh = 0;
        private int _srcCountLow = 0;
        private int _srcWaiting = 0;

        #endregion

        #region Public Properties (for external access)

        /// <summary>
        /// Index of the last identified High point
        /// </summary>
        public int LastHighIndex => _lastHighIndex;

        /// <summary>
        /// Index of the last identified Low point
        /// </summary>
        public int LastLowIndex => _lastLowIndex;

        /// <summary>
        /// Value of the last identified High point
        /// </summary>
        public double LastHighValue => _lastHigh;

        /// <summary>
        /// Value of the last identified Low point
        /// </summary>
        public double LastLowValue => _lastLow;

        /// <summary>
        /// Type of the last identified point: -1 = High, 1 = Low, 0 = Initial
        /// </summary>
        public int LastType => _type;

        /// <summary>
        /// Series containing High ZigZag points (value > 0 at high points, 0 elsewhere)
        /// </summary>
        public IndicatorDataSeries HighZigZags => _highZigZags;

        /// <summary>
        /// Series containing Low ZigZag points (value > 0 at low points, 0 elsewhere)
        /// </summary>
        public IndicatorDataSeries LowZigZags => _lowZigZags;

        /// <summary>
        /// Count of High points identified
        /// </summary>
        public int CountHigh => _countHigh;

        /// <summary>
        /// Count of Low points identified
        /// </summary>
        public int CountLow => _countLow;

        // ===== Multi-Timeframe Public Access =====

        /// <summary>
        /// The source bars used for ZigZag computation (15min, 1h, etc.)
        /// When SourceTimeFrame == chart TF, this equals Bars.
        /// </summary>
        public Bars SourceBars => _sourceBars;

        /// <summary>
        /// Whether multi-timeframe mode is active
        /// </summary>
        public bool IsMultiTimeFrame => _useSourceBars;

        /// <summary>
        /// Source-indexed High ZigZag points (value > 0 at high points)
        /// </summary>
        public double[] SourceHighZigZags => _srcHighZZ;

        /// <summary>
        /// Source-indexed Low ZigZag points (value > 0 at low points)
        /// </summary>
        public double[] SourceLowZigZags => _srcLowZZ;

        /// <summary>
        /// Last processed source bar index
        /// </summary>
        public int LastProcessedSourceIndex => _lastProcessedSourceIdx;

        #endregion

        #region Indicator Events

        protected override void Initialize()
        {

            Print("{0} : {1}", NAME, VERSION);

            _highZigZags = CreateDataSeries();
            _lowZigZags = CreateDataSeries();
            _point = Symbol.TickSize;

            // Multi-Timeframe setup
            if (SourceTimeFrame != null && SourceTimeFrame != TimeFrame)
            {
                _sourceBars = MarketData.GetBars(SourceTimeFrame);
                _useSourceBars = true;
                Print("ZigZagExt: Multi-TF mode active. Source={0}, Chart={1}", SourceTimeFrame, TimeFrame);
            }
            else
            {
                _sourceBars = Bars;
                _useSourceBars = false;
            }

            // Initialize source arrays
            _srcHighZZ = new double[Math.Max(_sourceBars.Count + 100, 1000)];
            _srcLowZZ = new double[Math.Max(_sourceBars.Count + 100, 1000)];

        }

        public override void Calculate(int index)
        {
            // ===== Multi-Timeframe Processing =====
            if (_useSourceBars)
            {
                // Find the source bar index corresponding to this chart bar
                var chartBarTime = Bars.OpenTimes[index];
                var sourceIndex = _sourceBars.OpenTimes.GetIndexByTime(chartBarTime);

                // Ensure arrays are large enough
                if (sourceIndex >= _srcHighZZ.Length)
                    EnsureSourceArraySize(sourceIndex + 100);

                // Process any new source bars since last call
                while (_lastProcessedSourceIdx < sourceIndex)
                {
                    _lastProcessedSourceIdx++;
                    ProcessSourceBar(_lastProcessedSourceIdx);
                }

                // Map source results to chart for display:
                // If the current source bar has a ZigZag point, show it on the chart
                if (sourceIndex >= 0 && sourceIndex < _srcHighZZ.Length)
                {
                    if (_srcHighZZ[sourceIndex] > 0)
                    {
                        Result[index] = _srcHighZZ[sourceIndex];
                        _highZigZags[index] = _srcHighZZ[sourceIndex];
                    }
                    else if (_srcLowZZ[sourceIndex] > 0)
                    {
                        Result[index] = _srcLowZZ[sourceIndex];
                        _lowZigZags[index] = _srcLowZZ[sourceIndex];
                    }
                    else
                    {
                        // No ZigZag point at this source bar, clear chart values
                        _highZigZags[index] = 0;
                        _lowZigZags[index] = 0;
                    }
                }

                return; // Skip standard chart-based processing
            }

            // ===== Standard Single-Timeframe Processing =====
            switch (MyModeZigZag)
            {

                case ModeZigZag.HighLow:

                    PerformIndicatorHighLow(index);

                    break;

                case ModeZigZag.OpenClose:

                    PerformIndicatorOpenClose(index);

                    break;

            }

            if (_highZigZags[index] == _lastHigh && _waiting > -1)
            {

                _waiting = -1;
                _countHigh++;

            }
            
            if (_lowZigZags[index] == _lastLow && _waiting < 1)
            {

                _waiting = 1;
                _countLow++;

            }

            if (ShowLabel && Chart != null)
            {

                if (_highZigZags[index] > 0)
                {

                    ChartText cth = Chart.DrawText("zzh-" + _countHigh, _highZigZags[_lastHighIndex].ToString("N" + Symbol.Digits), _lastHighIndex, _highZigZags[_lastHighIndex], ColorHigh);
                    cth.HorizontalAlignment = HorizontalAlignment.Center;
                    cth.VerticalAlignment = VerticalAlignment.Top;

                }

                if (_lowZigZags[index] > 0)
                {

                    ChartText ctl = Chart.DrawText("zzl-" + _countLow, _lowZigZags[_lastLowIndex].ToString("N" + Symbol.Digits), _lastLowIndex, _lowZigZags[_lastLowIndex], ColorLow);
                    ctl.HorizontalAlignment = HorizontalAlignment.Center;
                    ctl.VerticalAlignment = VerticalAlignment.Bottom;

                }

            }            

        }

        #endregion

        #region Source Bar Processing (Multi-Timeframe)

        /// <summary>
        /// Ensures the source arrays are at least the specified size
        /// </summary>
        private void EnsureSourceArraySize(int minSize)
        {
            if (_srcHighZZ.Length < minSize)
            {
                var newSize = Math.Max(minSize, _srcHighZZ.Length * 2);
                var newHigh = new double[newSize];
                var newLow = new double[newSize];
                Array.Copy(_srcHighZZ, newHigh, _srcHighZZ.Length);
                Array.Copy(_srcLowZZ, newLow, _srcLowZZ.Length);
                _srcHighZZ = newHigh;
                _srcLowZZ = newLow;
            }
        }

        /// <summary>
        /// Processes a single source bar through the ZigZag algorithm.
        /// This is identical to PerformIndicatorHighLow() but uses _sourceBars and _srcXXX state.
        /// </summary>
        private void ProcessSourceBar(int index)
        {
            if (index < Depth)
            {
                _srcHighZZ[index] = 0;
                _srcLowZZ[index] = 0;
                return;
            }

            // Find minimum low in last Depth bars of source
            double currentLow = double.MaxValue;
            for (int i = Math.Max(0, index - Depth + 1); i <= index; i++)
                if (_sourceBars.LowPrices[i] < currentLow)
                    currentLow = _sourceBars.LowPrices[i];

            if (Math.Abs(currentLow - _srcLastLow) < double.Epsilon)
                currentLow = 0.0;
            else
            {
                _srcLastLow = currentLow;
                if ((_sourceBars.LowPrices[index] - currentLow) > (Deviation * _point))
                    currentLow = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (index - i >= 0 && Math.Abs(_srcLowZZ[index - i]) > double.Epsilon && _srcLowZZ[index - i] > currentLow)
                            _srcLowZZ[index - i] = 0.0;
                    }
                }
            }
            if (Math.Abs(_sourceBars.LowPrices[index] - currentLow) < double.Epsilon)
                _srcLowZZ[index] = currentLow;
            else
                _srcLowZZ[index] = 0.0;

            // Find maximum high in last Depth bars of source
            double currentHigh = double.MinValue;
            for (int i = Math.Max(0, index - Depth + 1); i <= index; i++)
                if (_sourceBars.HighPrices[i] > currentHigh)
                    currentHigh = _sourceBars.HighPrices[i];

            if (Math.Abs(currentHigh - _srcLastHigh) < double.Epsilon)
                currentHigh = 0.0;
            else
            {
                _srcLastHigh = currentHigh;
                if ((currentHigh - _sourceBars.HighPrices[index]) > (Deviation * _point))
                    currentHigh = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (index - i >= 0 && Math.Abs(_srcHighZZ[index - i]) > double.Epsilon && _srcHighZZ[index - i] < currentHigh)
                            _srcHighZZ[index - i] = 0.0;
                    }
                }
            }
            if (Math.Abs(_sourceBars.HighPrices[index] - currentHigh) < double.Epsilon)
                _srcHighZZ[index] = currentHigh;
            else
                _srcHighZZ[index] = 0.0;

            // ZigZag direction tracking (same logic as PerformIndicatorHighLow)
            switch (_srcType)
            {
                case 0:
                    if (Math.Abs(_srcLow - 0) < double.Epsilon && Math.Abs(_srcHigh - 0) < double.Epsilon)
                    {
                        if (Math.Abs(_srcHighZZ[index]) > double.Epsilon)
                        {
                            _srcHigh = _sourceBars.HighPrices[index];
                            _srcLastHighIndex = index;
                            _srcType = -1;
                        }
                        if (Math.Abs(_srcLowZZ[index]) > double.Epsilon)
                        {
                            _srcLow = _sourceBars.LowPrices[index];
                            _srcLastLowIndex = index;
                            _srcType = 1;
                        }
                    }
                    break;
                case 1:
                    if (Math.Abs(_srcLowZZ[index]) > double.Epsilon && _srcLowZZ[index] < _srcLow && Math.Abs(_srcHighZZ[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous source low; never erase a mapped point.
                        _srcLastLowIndex = index;
                        _srcLow = _srcLowZZ[index];
                    }
                    if (Math.Abs(_srcHighZZ[index] - 0.0) > double.Epsilon && Math.Abs(_srcLowZZ[index] - 0.0) < double.Epsilon)
                    {
                        _srcHigh = _srcHighZZ[index];
                        _srcLastHighIndex = index;
                        _srcType = -1;
                    }
                    break;
                case -1:
                    if (Math.Abs(_srcHighZZ[index]) > double.Epsilon && _srcHighZZ[index] > _srcHigh && Math.Abs(_srcLowZZ[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous source high; never erase a mapped point.
                        _srcLastHighIndex = index;
                        _srcHigh = _srcHighZZ[index];
                    }
                    if (Math.Abs(_srcLowZZ[index]) > double.Epsilon && Math.Abs(_srcHighZZ[index]) < double.Epsilon)
                    {
                        _srcLow = _srcLowZZ[index];
                        _srcLastLowIndex = index;
                        _srcType = 1;
                    }
                    break;
                default:
                    return;
            }

            // Counter tracking
            if (_srcHighZZ[index] == _srcLastHigh && _srcWaiting > -1)
            {
                _srcWaiting = -1;
                _srcCountHigh++;
            }
            if (_srcLowZZ[index] == _srcLastLow && _srcWaiting < 1)
            {
                _srcWaiting = 1;
                _srcCountLow++;
            }
        }

        #endregion

        #region Private Methods

        private void PerformIndicatorHighLow(int index)
        {

            if (index < Depth)
            {
                Result[index] = 0;
                _highZigZags[index] = 0;
                _lowZigZags[index] = 0;
                return;
            }

            _currentLow = Functions.Minimum(Bars.LowPrices, Depth);

            if (Math.Abs(_currentLow - _lastLow) < double.Epsilon)
                _currentLow = 0.0;
            else
            {

                _lastLow = _currentLow;

                if ((Bars.LowPrices[index] - _currentLow) > (Deviation * _point))
                    _currentLow = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (Math.Abs(_lowZigZags[index - i]) > double.Epsilon && _lowZigZags[index - i] > _currentLow)
                            _lowZigZags[index - i] = 0.0;
                    }
                }
            }
            if (Math.Abs(Bars.LowPrices[index] - _currentLow) < double.Epsilon)
                _lowZigZags[index] = _currentLow;
            else
                _lowZigZags[index] = 0.0;

            _currentHigh = Bars.HighPrices.Maximum(Depth);

            if (Math.Abs(_currentHigh - _lastHigh) < double.Epsilon)
                _currentHigh = 0.0;
            else
            {

                _lastHigh = _currentHigh;

                if ((_currentHigh - Bars.HighPrices[index]) > (Deviation * _point))
                    _currentHigh = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (Math.Abs(_highZigZags[index - i]) > double.Epsilon && _highZigZags[index - i] < _currentHigh)
                            _highZigZags[index - i] = 0.0;
                    }
                }
            }

            if (Math.Abs(Bars.HighPrices[index] - _currentHigh) < double.Epsilon)
                _highZigZags[index] = _currentHigh;
            else
                _highZigZags[index] = 0.0;


            switch (_type)
            {
                case 0:
                    if (Math.Abs(_low - 0) < double.Epsilon && Math.Abs(_high - 0) < double.Epsilon)
                    {
                        if (Math.Abs(_highZigZags[index]) > double.Epsilon)
                        {
                            _high = Bars.HighPrices[index];
                            _lastHighIndex = index;
                            _type = -1;
                            Result[index] = _high;
                        }
                        if (Math.Abs(_lowZigZags[index]) > double.Epsilon)
                        {
                            _low = Bars.LowPrices[index];
                            _lastLowIndex = index;
                            _type = 1;
                            Result[index] = _low;
                        }
                    }
                    break;
                case 1:
                    if (Math.Abs(_lowZigZags[index]) > double.Epsilon && _lowZigZags[index] < _low && Math.Abs(_highZigZags[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous low drawn; never erase a painted point.
                        _lastLowIndex = index;
                        _low = _lowZigZags[index];
                        Result[index] = _low;
                    }
                    if (Math.Abs(_highZigZags[index] - 0.0) > double.Epsilon && Math.Abs(_lowZigZags[index] - 0.0) < double.Epsilon)
                    {
                        _high = _highZigZags[index];
                        _lastHighIndex = index;
                        Result[index] = _high;
                        _type = -1;
                    }
                    break;
                case -1:
                    if (Math.Abs(_highZigZags[index]) > double.Epsilon && _highZigZags[index] > _high && Math.Abs(_lowZigZags[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous high drawn; never erase a painted point.
                        _lastHighIndex = index;
                        _high = _highZigZags[index];
                        Result[index] = _high;
                    }
                    if (Math.Abs(_lowZigZags[index]) > double.Epsilon && Math.Abs(_highZigZags[index]) < double.Epsilon)
                    {
                        _low = _lowZigZags[index];
                        _lastLowIndex = index;
                        Result[index] = _low;
                        _type = 1;
                    }
                    break;
                default:
                    return;
            }

        }

        private void PerformIndicatorOpenClose(int index)
        {

            if (index < Depth)
            {
                Result[index] = 0;
                _highZigZags[index] = 0;
                _lowZigZags[index] = 0;
                return;
            }

            _currentLow = Functions.Minimum(Bars.OpenPrices, Depth);

            if (Math.Abs(_currentLow - _lastLow) < double.Epsilon)
                _currentLow = 0.0;
            else
            {

                _lastLow = _currentLow;

                if ((Bars.OpenPrices[index] - _currentLow) > (Deviation * _point))
                    _currentLow = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (Math.Abs(_lowZigZags[index - i]) > double.Epsilon && _lowZigZags[index - i] > _currentLow)
                            _lowZigZags[index - i] = 0.0;
                    }
                }
            }
            if (Math.Abs(Bars.OpenPrices[index] - _currentLow) < double.Epsilon)
                _lowZigZags[index] = _currentLow;
            else
                _lowZigZags[index] = 0.0;

            _currentHigh = Bars.OpenPrices.Maximum(Depth);

            if (Math.Abs(_currentHigh - _lastHigh) < double.Epsilon)
                _currentHigh = 0.0;
            else
            {

                _lastHigh = _currentHigh;

                if ((_currentHigh - Bars.OpenPrices[index]) > (Deviation * _point))
                    _currentHigh = 0.0;
                else
                {
                    for (int i = 1; i <= BackStep; i++)
                    {
                        if (Math.Abs(_highZigZags[index - i]) > double.Epsilon && _highZigZags[index - i] < _currentHigh)
                            _highZigZags[index - i] = 0.0;
                    }
                }
            }

            if (Math.Abs(Bars.OpenPrices[index] - _currentHigh) < double.Epsilon)
                _highZigZags[index] = _currentHigh;
            else
                _highZigZags[index] = 0.0;


            switch (_type)
            {
                case 0:
                    if (Math.Abs(_low - 0) < double.Epsilon && Math.Abs(_high - 0) < double.Epsilon)
                    {
                        if (Math.Abs(_highZigZags[index]) > double.Epsilon)
                        {
                            _high = Bars.OpenPrices[index];
                            _lastHighIndex = index;
                            _type = -1;
                            Result[index] = _high;
                        }
                        if (Math.Abs(_lowZigZags[index]) > double.Epsilon)
                        {
                            _low = Bars.OpenPrices[index];
                            _lastLowIndex = index;
                            _type = 1;
                            Result[index] = _low;
                        }
                    }
                    break;
                case 1:
                    if (Math.Abs(_lowZigZags[index]) > double.Epsilon && _lowZigZags[index] < _low && Math.Abs(_highZigZags[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous low drawn; never erase a painted point.
                        _lastLowIndex = index;
                        _low = _lowZigZags[index];
                        Result[index] = _low;
                    }
                    if (Math.Abs(_highZigZags[index] - 0.0) > double.Epsilon && Math.Abs(_lowZigZags[index] - 0.0) < double.Epsilon)
                    {
                        _high = _highZigZags[index];
                        _lastHighIndex = index;
                        Result[index] = _high;
                        _type = -1;
                    }
                    break;
                case -1:
                    if (Math.Abs(_highZigZags[index]) > double.Epsilon && _highZigZags[index] > _high && Math.Abs(_lowZigZags[index] - 0.0) < double.Epsilon)
                    {
                        // No-repaint: keep the previous high drawn; never erase a painted point.
                        _lastHighIndex = index;
                        _high = _highZigZags[index];
                        Result[index] = _high;
                    }
                    if (Math.Abs(_lowZigZags[index]) > double.Epsilon && Math.Abs(_highZigZags[index]) < double.Epsilon)
                    {
                        _low = _lowZigZags[index];
                        _lastLowIndex = index;
                        Result[index] = _low;
                        _type = 1;
                    }
                    break;
                default:
                    return;
            }

        }

        #endregion


    }

}
