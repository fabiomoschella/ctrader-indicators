using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SuperTrendMTFFinal : Indicator
    {
        [Parameter("ATR Period", DefaultValue = 10, MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("Multiplier", DefaultValue = 3.0, MinValue = 0.1)]
        public double Multiplier { get; set; }

        private Bars[] _bars;
        private AverageTrueRange[] _atrs;
        private bool[] _isBullish; // Rinominato per chiarezza
        private double[] _upperBand;
        private double[] _lowerBand;
        private int[] _trend; // 1 per Bullish, -1 per Bearish

        private readonly string[] _labels = { "D1", "H4", "H1", "M15", "M5" };
        private readonly TimeFrame[] _timeframes =
        {
            TimeFrame.Daily,
            TimeFrame.Hour4,
            TimeFrame.Hour,
            TimeFrame.Minute15,
            TimeFrame.Minute5
        };

        protected override void Initialize()
        {
            _bars = new Bars[5];
            _atrs = new AverageTrueRange[5];
            _isBullish = new bool[5];
            _upperBand = new double[5];
            _lowerBand = new double[5];
            _trend = new int[5];

            for (int i = 0; i < 5; i++)
            {
                _bars[i] = MarketData.GetBars(_timeframes[i]);
                _atrs[i] = Indicators.AverageTrueRange(_bars[i], AtrPeriod, MovingAverageType.WilderSmoothing);
                // Inizializza il trend a 0 per il primo calcolo
                _trend[i] = 0;
            }
        }

        public override void Calculate(int index)
        {
            // Calcola solo sull'ultima barra per efficienza
            if (!IsLastBar) return;

            for (int i = 0; i < 5; i++)
            {
                int barIndex = _bars[i].Count - 2; // Indice della penultima barra per il confronto
                if (barIndex < AtrPeriod) continue;

                double high = _bars[i].HighPrices[barIndex];
                double low = _bars[i].LowPrices[barIndex];
                double close = _bars[i].ClosePrices[barIndex];
                double atr = _atrs[i].Result[barIndex];

                double hl2 = (high + low) / 2;
                double currentUpper = hl2 + (Multiplier * atr);
                double currentLower = hl2 - (Multiplier * atr);

                // Logica Supertrend Standard
                if (_trend[i] == 1 || _trend[i] == 0) // Se il trend precedente era rialzista o non definito
                {
                    _upperBand[i] = currentUpper;
                    _lowerBand[i] = (currentLower > _lowerBand[i]) ? currentLower : _lowerBand[i]; // La banda inferiore può solo salire in un trend rialzista
                }

                if (_trend[i] == -1 || _trend[i] == 0) // Se il trend precedente era ribassista o non definito
                {
                    _lowerBand[i] = currentLower;
                    _upperBand[i] = (currentUpper < _upperBand[i]) ? currentUpper : _upperBand[i]; // La banda superiore può solo scendere in un trend ribassista
                }

                // Determina la direzione del trend
                if (close > _upperBand[i])
                {
                    _trend[i] = 1; // Trend diventa rialzista
                }
                else if (close < _lowerBand[i])
                {
                    _trend[i] = -1; // Trend diventa ribassista
                }

                // Ottieni la chiusura della barra corrente per l'aggiornamento finale
                double lastClose = _bars[i].ClosePrices.LastValue;
                if (_trend[i] == 1 && lastClose < _lowerBand[i])
                {
                    _trend[i] = -1;
                }
                else if (_trend[i] == -1 && lastClose > _upperBand[i])
                {
                     _trend[i] = 1;
                }

                _isBullish[i] = _trend[i] == 1;
            }

            DisplayPanel();
        }

        private void DisplayPanel()
        {
            string text = "╔═══════════════╗\n";
            text += "║ SUPERTREND MTF  ║\n";
            text += "╠═══════════════╣\n";

            int bullCount = 0;
            for (int i = 0; i < 5; i++)
            {
                if (_isBullish[i]) bullCount++;
            }

            int consensus = (bullCount * 100) / 5;
            text += $"║ Consensus:{consensus,3}% ║\n";
            text += "╠═══════════════╣\n";

            for (int i = 0; i < 5; i++)
            {
                string arrow = _isBullish[i] ? "▲" : "▼";
                string dir = _isBullish[i] ? "UP" : "DN";
                // Aggiusta la spaziatura per un allineamento corretto
                text += $"║ {_labels[i],-3} {arrow} {dir}         ║\n";
            }

            text += "╚═══════════════╝";

            Color color = Color.Gray; // Colore di default
            if (consensus == 100) color = Color.Lime;
            else if (consensus == 80) color = Color.Green;
            else if (consensus == 60) color = Color.YellowGreen;
            else if (consensus == 40) color = Color.Orange;
            else if (consensus == 20) color = Color.OrangeRed;
            else if (consensus == 0) color = Color.Red;


            Chart.DrawStaticText("STMTF", text, VerticalAlignment.Top, HorizontalAlignment.Right, color);
        }
    }
}