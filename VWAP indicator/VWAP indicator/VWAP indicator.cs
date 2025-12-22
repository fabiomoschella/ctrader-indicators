using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
[Indicator(IsOverlay = true, AutoRescale = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
public class SampleVWAP : Indicator {
    [Parameter("Source")]
    public DataSeries Source { get; set; }

    [Output("Main", LineColor = "Orange", Thickness = 2)]
    public IndicatorDataSeries Result { get; set; }

    private TypicalPrice typ;
    public IndicatorDataSeries tpv;
    public double CTPV, CV;

    protected override void Initialize() {
        typ = Indicators.TypicalPrice();
        tpv = CreateDataSeries();
    }

    public override void Calculate(int index) {
        tpv[index] = typ.Result[index] * Bars.TickVolumes[index];
        CTPV = 0;
        CV = 0;

        int per = 0;
        int day = Bars.OpenTimes[index].Day;
        while (Bars.OpenTimes[index - per].Day == day) {
            per++;
            CTPV += tpv[index - per];
            CV += MarketSeries.TickVolume[index - per];
        }

        Result[index] = CTPV / CV;
    }
}
}