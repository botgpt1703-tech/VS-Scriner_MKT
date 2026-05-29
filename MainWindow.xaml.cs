using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using ScottPlot.WPF;
using Binance.Net.Enums;
using ScottPlot;

namespace TradingScreener
{
    public partial class MainWindow : Window
    {
        private TradingDataService _service = new();
        private CancellationTokenSource[] _cts = new CancellationTokenSource[4];
        private string[] _currentSymbols = { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT" };
        private Dictionary<string, List<OHLC>> _historyCache = new();
        private Dictionary<string, List<double>> _oiHistoryCache = new();

        public MainWindow()
        {
            InitializeComponent();
            // Запускаем всё при старте
            LoadChart(0, _currentSymbols[0], Chart1, OiChart1, Label1, KlineInterval.OneMinute);
            LoadChart(1, _currentSymbols[1], Chart2, OiChart2, Label2, KlineInterval.OneMinute);
            LoadChart(2, _currentSymbols[2], Chart3, OiChart3, Label3, KlineInterval.OneMinute);
            LoadChart(3, _currentSymbols[3], Chart4, OiChart4, Label4, KlineInterval.OneMinute);
        }

        private async void LoadChart(int idx, string symbol, WpfPlot chart, WpfPlot oiChart, TextBlock label, KlineInterval interval)
        {
            _cts[idx]?.Cancel();
            _cts[idx] = new CancellationTokenSource();
            var token = _cts[idx].Token;

            chart.Plot.Clear();
            chart.Refresh();

            List<OHLC> candles;
            string cacheKey = $"{symbol}_{interval}";

            if (_historyCache.ContainsKey(cacheKey)) candles = _historyCache[cacheKey];
            else
            {
                candles = await _service.GetHistoryAsync(symbol, interval);
                if (candles != null && candles.Count > 0) _historyCache[cacheKey] = candles;
            }

            if (token.IsCancellationRequested || candles == null) return;

            UpdateChartUI(chart, candles, true);
            UpdateOiChart(oiChart, symbol); // Первая загрузка OI

            await _service.StartStreaming(symbol, interval, candle => {
                if (token.IsCancellationRequested) return;
                Dispatcher.BeginInvoke(() => {
                    if (candles.Count > 0 && candles.Last().DateTime == candle.DateTime)
                        candles[candles.Count - 1] = candle;
                    else
                        candles.Add(candle);
                    UpdateChartUI(chart, candles, false);
                    UpdateOiChart(oiChart, symbol); // Обновление OI при новой свече
                });
            }, price => Dispatcher.BeginInvoke(() => label.Text = $"{symbol}: {price:F2}"));
        }

        private async void UpdateOiChart(WpfPlot oiChart, string symbol)
        {
            decimal currentOi = await _service.GetOpenInterestAsync(symbol);

            string key = $"{symbol}_OI";
            if (!_oiHistoryCache.ContainsKey(key)) _oiHistoryCache[key] = new List<double>();

            var history = _oiHistoryCache[key];
            history.Add((double)currentOi);
            if (history.Count > 50) history.RemoveAt(0);

            oiChart.Plot.Clear();

            double[] xs = Enumerable.Range(0, history.Count).Select(i => (double)i).ToArray();
            double[] ys = history.ToArray();

            // 1. Создаем список координат
            List<ScottPlot.Coordinates> polyPoints = new List<ScottPlot.Coordinates>();
            polyPoints.Add(new ScottPlot.Coordinates(xs[0], 0));
            for (int i = 0; i < xs.Length; i++) polyPoints.Add(new ScottPlot.Coordinates(xs[i], ys[i]));
            polyPoints.Add(new ScottPlot.Coordinates(xs.Last(), 0));

            // 2. ВАЖНО: Добавляем .ToArray() перед передачей в Add.Polygon
            var poly = oiChart.Plot.Add.Polygon(polyPoints.ToArray());
            poly.FillColor = ScottPlot.Colors.Cyan.WithAlpha(0.2);
            poly.LineColor = ScottPlot.Colors.Transparent;

            // 3. Линия
            var line = oiChart.Plot.Add.Scatter(xs, ys);
            line.MarkerSize = 0;
            line.LineWidth = 2;
            line.Color = ScottPlot.Colors.Cyan;

            oiChart.Plot.Axes.AutoScale();
            oiChart.Refresh();
        }

        private void UpdateChartUI(WpfPlot chart, List<OHLC> candles, bool isInitialLoad)
        {
            if (candles == null || candles.Count == 0) return;
            chart.Plot.Clear();
            chart.Plot.Add.Candlestick(candles.ToArray());
            if (isInitialLoad) chart.Plot.Axes.AutoScale();
            chart.Refresh();
        }

        private void ChangeSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                TextBox input = tag switch { "1" => Input1, "2" => Input2, "3" => Input3, "4" => Input4, _ => null };
                WpfPlot chart = tag switch { "1" => Chart1, "2" => Chart2, "3" => Chart3, "4" => Chart4, _ => null };
                WpfPlot oi = tag switch { "1" => OiChart1, "2" => OiChart2, "3" => OiChart3, "4" => OiChart4, _ => null };
                TextBlock label = tag switch { "1" => Label1, "2" => Label2, "3" => Label3, "4" => Label4, _ => null };

                if (input != null && chart != null && oi != null)
                {
                    _currentSymbols[int.Parse(tag) - 1] = input.Text.ToUpper();
                    LoadChart(int.Parse(tag) - 1, _currentSymbols[int.Parse(tag) - 1], chart, oi, label, KlineInterval.OneMinute);
                }
            }
        }

        private void TimeframeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && cb.IsLoaded)
            {
                string tag = cb.Name.Replace("Combo", "");
                int idx = int.Parse(tag) - 1;
                WpfPlot chart = tag switch { "1" => Chart1, "2" => Chart2, "3" => Chart3, "4" => Chart4, _ => null };
                WpfPlot oi = tag switch { "1" => OiChart1, "2" => OiChart2, "3" => OiChart3, "4" => OiChart4, _ => null };
                TextBlock label = tag switch { "1" => Label1, "2" => Label2, "3" => Label3, "4" => Label4, _ => null };

                if (chart != null && oi != null)
                {
                    KlineInterval interval = item.Content.ToString() switch
                    {
                        "5m" => KlineInterval.FiveMinutes,
                        "15m" => KlineInterval.FifteenMinutes,
                        "1h" => KlineInterval.OneHour,
                        _ => KlineInterval.OneMinute
                    };
                    LoadChart(idx, _currentSymbols[idx], chart, oi, label, interval);
                }
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                WpfPlot chart = tag switch { "1" => Chart1, "2" => Chart2, "3" => Chart3, "4" => Chart4, _ => null };
                chart?.Plot.Axes.AutoScale();
                chart?.Refresh();
            }
        }
    }
}