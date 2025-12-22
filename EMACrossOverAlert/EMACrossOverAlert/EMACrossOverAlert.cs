using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class EMACrossoverAlert : Indicator
    {
        [Parameter("EMA veloce", DefaultValue = 12)]
        public int FastEmaPeriod { get; set; }

        [Parameter("EMA lenta", DefaultValue = 24)]
        public int SlowEmaPeriod { get; set; }
        
        [Parameter("Attiva alert sonoro", DefaultValue = true)]
        public bool EnableSoundAlert { get; set; }

        [Output("EMA veloce", PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries FastEma { get; set; }

        [Output("EMA lenta", PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries SlowEma { get; set; }

        private ExponentialMovingAverage fastEmaIndicator;
        private ExponentialMovingAverage slowEmaIndicator;
        private bool wasLongSignal = false;
        private bool wasShortSignal = false;

        protected override void Initialize()
        {
            fastEmaIndicator = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            slowEmaIndicator = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
        }

        public override void Calculate(int index)
        {
            FastEma[index] = fastEmaIndicator.Result[index];
            SlowEma[index] = slowEmaIndicator.Result[index];

            // Verifica che ci siano almeno 2 barre calcolate
            if (index < 1)
                return;

            // Controllo per crossover al rialzo (segnale long)
            bool isLongSignal = FastEma[index] > SlowEma[index] && FastEma[index - 1] <= SlowEma[index - 1];
            
            // Controllo per crossover al ribasso (segnale short)
            bool isShortSignal = FastEma[index] < SlowEma[index] && FastEma[index - 1] >= SlowEma[index - 1];

            // Evita segnali duplicati
            bool validLongSignal = isLongSignal && !wasLongSignal;
            bool validShortSignal = isShortSignal && !wasShortSignal;

            // Resetta i flag quando non c'è segnale attuale
            if (!isLongSignal)
                wasLongSignal = false;
            
            if (!isShortSignal)
                wasShortSignal = false;

            // Disegna una freccia blu per segnale long
            if (validLongSignal)
            {
                Chart.DrawIcon("LongSignal" + index, ChartIconType.UpArrow, index, Bars.LowPrices[index] - 10 * Symbol.PipSize, Color.Blue);
                wasLongSignal = true;
                
                // Crea un alert
                Chart.DrawStaticText("Alert" + index, "EMA CROSSOVER: Segnale LONG rilevato!", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Blue);
                
                // Attiva alert sonoro se abilitato
                if (EnableSoundAlert)
                {
                    Notifications.PlaySound("Alert");
                }
            }

            // Disegna una freccia rossa per segnale short
            if (validShortSignal)
            {
                Chart.DrawIcon("ShortSignal" + index, ChartIconType.DownArrow, index, Bars.HighPrices[index] + 10 * Symbol.PipSize, Color.Red);
                wasShortSignal = true;
                
                // Crea un alert
                Chart.DrawStaticText("Alert" + index, "EMA CROSSOVER: Segnale SHORT rilevato!", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Red);
                
                // Attiva alert sonoro se abilitato
                if (EnableSoundAlert)
                {
                    Notifications.PlaySound("Alert");
                }
            }
        }
    }
}