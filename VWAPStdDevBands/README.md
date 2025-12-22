# VWAP Standard Deviation Bands

A custom cTrader indicator that displays VWAP (Volume Weighted Average Price) with configurable Standard Deviation Bands.

## Features

- **VWAP Calculation**: Volume-weighted average price with daily reset
- **Multiple Deviation Bands**: Three levels of standard deviation bands (S1, S2, S3)
- **Configurable Multipliers**: Customize deviation band distances (default: 1σ, 2σ, 3σ)
- **Flexible Display**: Show/hide individual bands and VWAP line
- **Price Source Options**: Choose from Typical, Close, Open, High, Low, Median, or Weighted price for deviation calculation

## Building

```bash
cd VWAPStdDevBands
dotnet build VWAPStdDevBands.csproj
```

The compiled `.algo` file will be generated in the output directory and can be loaded directly into cTrader.

## Parameters

- **S1/S2/S3 Multiplier**: Standard deviation multipliers for each band level
- **Show VWAP**: Toggle VWAP line visibility
- **Show S1/S2/S3 Bands**: Toggle individual band visibility
- **Price Source**: Select which price to use for standard deviation calculation

## Installation

1. Build the project using the command above
2. The `.algo` file will be automatically copied to the cTrader indicators folder
3. Restart cTrader or refresh indicators
4. Add the indicator to your chart

## Technical Details

- **Target Framework**: .NET 6.0
- **Platform**: cTrader/cAlgo
- **Daily Reset**: VWAP recalculates at the start of each trading day
- **Volume**: Uses tick volume for VWAP calculation

## License

Open source - feel free to modify and use as needed.
