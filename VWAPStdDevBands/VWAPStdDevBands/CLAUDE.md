# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **cTrader/cAlgo custom indicator** that displays VWAP (Volume Weighted Average Price) with Standard Deviation Bands. The indicator overlays on price charts and calculates volume-weighted price levels with configurable standard deviation bands (S1, S2, S3).

## Build and Development

### Building the Project
```bash
dotnet build VWAPStdDevBands.csproj
```

### Project Structure
- **Target Framework**: .NET 6.0
- **Main Dependency**: cTrader.Automate (NuGet package)
- **Single File Architecture**: All indicator logic is in [VWAPStdDevBands.cs](VWAPStdDevBands.cs)

### Testing in cTrader
This indicator must be tested within the cTrader platform. After building:
1. Copy the compiled DLL to cTrader's custom indicators folder
2. Restart cTrader or refresh indicators
3. Apply to a chart to verify calculations

## Indicator Architecture

### Calculation Flow
The indicator recalculates on every bar update following this sequence:

1. **Daily Reset Detection** ([GetDayStartIndex:203-216](VWAPStdDevBands.cs#L203-L216))
   - Scans backwards to find the first bar of the current day
   - VWAP resets at the start of each new trading day

2. **VWAP Calculation** ([CalculateVWAP:182-201](VWAPStdDevBands.cs#L182-L201))
   - Formula: `VWAP = Σ(Typical Price × Volume) / Σ(Volume)`
   - Typical Price = (High + Low + Close) / 3
   - Accumulates from day start index to current bar

3. **Standard Deviation** ([CalculateStandardDeviation:218-237](VWAPStdDevBands.cs#L218-L237))
   - Calculates deviation of the selected price source from VWAP
   - Formula: `σ = sqrt((1/n) × Σ(price - VWAP)²)`
   - Price source is configurable (Typical, Close, Open, High, Low, Median, Weighted)

4. **Band Generation** ([Calculate:123-154](VWAPStdDevBands.cs#L123-L154))
   - Three band levels: S1 (1σ), S2 (2σ), S3 (3σ)
   - Each band has upper and lower values: `VWAP ± (multiplier × σ)`
   - Bands can be individually shown/hidden via parameters

### Key Components

**Parameters** ([lines 10-57](VWAPStdDevBands.cs#L10-L57))
- Standard deviation multipliers for S1, S2, S3 bands
- Show/hide toggles for each band level
- Price source selection for deviation calculation
- Customizable colors for each band

**Outputs** ([lines 59-77](VWAPStdDevBands.cs#L59-L77))
- Six IndicatorDataSeries: S1Upper, S1Lower, S2Upper, S2Lower, S3Upper, S3Lower
- When bands are hidden, values are set to `double.NaN` to prevent display

**Color Management** ([ApplyColors:249-258](VWAPStdDevBands.cs#L249-L258))
- Colors are applied in Initialize() from string parameters
- Supports both named colors and hex format (#RRGGBB)

## cTrader API Specifics

### Indicator Attributes
- `IsOverlay = true`: Indicator displays on the price chart
- `TimeZone = TimeZones.UTC`: All times are in UTC
- `AccessRights = AccessRights.None`: No file/network access required

### Data Access
- `Bars.ClosePrices[index]`, `Bars.OpenPrices[index]`, etc. - OHLC price data
- `Bars.TickVolumes[index]` - Volume data for each bar
- `Bars.OpenTimes[index]` - Bar timestamp for daily reset detection

### Calculate Method
The `Calculate(int index)` method is called for each bar in the chart:
- On historical bars during initialization
- On each new bar as it forms
- When parameters change

## Important Implementation Notes

1. **Index Safety**: Always check `if (index < 1)` before accessing previous bars to avoid index out of range errors.

2. **NaN Handling**: When VWAP calculation fails or bands are disabled, all band values must be set to `double.NaN` to prevent rendering artifacts.

3. **Daily Reset Logic**: The indicator recalculates VWAP from scratch for each day. This means early morning bars will have less stable VWAP values with fewer data points.

4. **Volume Data**: Uses `TickVolumes` which represents the number of price changes, not actual traded volume (real volume data may not be available on all instruments).

5. **Performance**: The nested loop structure (scanning backwards for day start, then calculating from day start to current bar) means performance can degrade on higher timeframes with many intraday bars. Consider this when modifying the calculation logic.
