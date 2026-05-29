using Binance.Net.Clients;
using Binance.Net.Enums;
using ScottPlot;
using Binance.Net.Interfaces;

namespace TradingScreener
{
    public class TradingDataService
    {
        private BinanceRestClient _restClient = new();
        private BinanceSocketClient _socketClient = new();

        public async Task<List<OHLC>> GetHistoryAsync(string symbol, KlineInterval interval)
        {
            var result = await _restClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, limit: 500);

            TimeSpan ts = interval switch
            {
                KlineInterval.FiveMinutes => TimeSpan.FromMinutes(5),
                KlineInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
                KlineInterval.OneHour => TimeSpan.FromHours(1),
                _ => TimeSpan.FromMinutes(1)
            };

            if (!result.Success) return new List<OHLC>();

            return result.Data.Select(k => new OHLC(
                (double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice,
                k.OpenTime, ts)).ToList();
        }

        // ЭТОТ МЕТОД БЫЛ ПОТЕРЯН
        public async Task StartStreaming(string symbol, KlineInterval interval, Action<OHLC> onCandle, Action<decimal> onPrice)
        {
            TimeSpan ts = interval switch
            {
                KlineInterval.FiveMinutes => TimeSpan.FromMinutes(5),
                KlineInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
                KlineInterval.OneHour => TimeSpan.FromHours(1),
                _ => TimeSpan.FromMinutes(1)
            };

            await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(symbol, interval, data =>
            {
                var k = data.Data.Data;
                onCandle(new OHLC((double)k.OpenPrice, (double)k.HighPrice, (double)k.LowPrice, (double)k.ClosePrice, k.OpenTime, ts));
                onPrice(k.ClosePrice);
            });
        }
        public async Task<decimal> GetOpenInterestAsync(string symbol)
        {
            var result = await _restClient.UsdFuturesApi.ExchangeData.GetOpenInterestAsync(symbol);
            return result.Success ? result.Data.OpenInterest : 0;
        }
    }
}