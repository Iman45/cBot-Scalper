//+------------------------------------------------------------------+
//|                                                  Smart Grid      |
//|                                      Copyright 2014, MD SAIF     |
//|                                   http://www.facebook.com/cls.fx |
//+------------------------------------------------------------------+
//-Grid trader cBot based on Bar-Time & Trend. For range market & 15 minute TimeFrame is best.

using System;
using System.Linq;
using cAlgo.API;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class cBotScalper : Robot
    {
        [Parameter("Buy", DefaultValue = true)]
        public bool Buy { get; set; }

        [Parameter("Sell", DefaultValue = true)]
        public bool Sell { get; set; }

        [Parameter("Pip Step", DefaultValue = 10, MinValue = 1)]
        public int PipStep { get; set; }

        [Parameter("First Volume", DefaultValue = 1000, MinValue = 1000, Step = 1000)]
        public int FirstVolume { get; set; }

        [Parameter("Volume Exponent", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double VolumeExponent { get; set; }

        [Parameter("Max Spread", DefaultValue = 3.0)]
        public double MaxSpread { get; set; }

        [Parameter("Take Profit Average", DefaultValue = 3, MinValue = 1)]
        public int TakeProfitAverage { get; set; }

        private string Label = "cls";
        private DateTime buyLastOpenTime;
        private DateTime sellLastOpenTime;
        private double spreedValue;
        private bool stopped = false;

        protected override void OnStart()
        {
        }

        protected override void OnTick()
        {
            try
            {
                spreedValue = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;

                if (TotalOpenPositions(TradeType.Buy) > 0)
                    f0_86(AveragePrice(TradeType.Buy), TakeProfitAverage);

                if (TotalOpenPositions(TradeType.Sell) > 0)
                    f0_88(AveragePrice(TradeType.Sell), TakeProfitAverage);

                if (MaxSpread >= spreedValue && !stopped)
                    OpenPosition();
            } catch (Exception e)
            {
                Print(e);

                throw;
            }
        }

        protected override void OnError(Error error)
        {
            if (error.Code == ErrorCode.NoMoney)
            {
                stopped = true;
                Print("openning stopped because: not enough money");
            }
        }

        private void OpenPosition()
        {
            if (Buy && TotalOpenPositions(TradeType.Buy) == 0 && MarketSeries.Close.Last(1) > MarketSeries.Close.Last(2))
            {
                var success = SendOrder(TradeType.Buy, FirstVolume);

                if (success)
                    buyLastOpenTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First BUY openning error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
            }

            if (Sell && TotalOpenPositions(TradeType.Sell) == 0 && MarketSeries.Close.Last(2) > MarketSeries.Close.Last(1))
            {
                var success = SendOrder(TradeType.Sell, FirstVolume);

                if (success)
                    sellLastOpenTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First SELL openning error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
            }

            N_28();
        }

        private void N_28()
        {
            if (TotalOpenPositions(TradeType.Buy) > 0)
            {
                if (Math.Round(Symbol.Ask, Symbol.Digits) < Math.Round(GetMinEntryPrice(TradeType.Buy) - PipStep * Symbol.PipSize, Symbol.Digits) && buyLastOpenTime != MarketSeries.OpenTime.Last(0))
                {
                    long volume = CalculateVolume(TradeType.Buy);

                    var success = SendOrder(TradeType.Buy, volume);

                    if (success)
                        buyLastOpenTime = MarketSeries.OpenTime.Last(0);
                    else
                        Print("Next BUY openning error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
                }
            }

            if (TotalOpenPositions(TradeType.Sell) > 0)
            {
                if (Math.Round(Symbol.Bid, Symbol.Digits) > Math.Round(GetMaxEntryPrice(TradeType.Sell) + PipStep * Symbol.PipSize, Symbol.Digits) && sellLastOpenTime != MarketSeries.OpenTime.Last(0))
                {
                    long volume = CalculateVolume(TradeType.Sell);

                    var success = SendOrder(TradeType.Sell, volume);

                    if (success)
                        sellLastOpenTime = MarketSeries.OpenTime.Last(0);
                    else
                        Print("Next SELL openning error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
                }
            }
        }

        private bool SendOrder(TradeType tradeType, long volume)
        {
            int cd_8 = 0;

            if (volume > 0)
            {
                TradeResult result = ExecuteMarketOrder(tradeType, Symbol, volume, Label, 0, 0, 0, "smart_grid");

                if (result.IsSuccessful)
                {
                    Print(tradeType, "Opened at: ", result.Position.EntryPrice);

                    return true;
                }
                else
                    Print(tradeType, "Openning Error: ", result.Error);
            }
            else
                Print("Volume calculation error: Calculated Volume is: ", volume);

            return false;
        }

        private void f0_86(double ai_4, int ad_8)
        {
            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TradeType.Buy)
                    {
                        double? li_16 = Math.Round(ai_4 + ad_8 * Symbol.PipSize, Symbol.Digits);
                        if (position.TakeProfit != li_16)
                            ModifyPosition(position, position.StopLoss, li_16);
                    }
                }
            }
        }

        private void f0_88(double ai_4, int ad_8)
        {
            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == TradeType.Sell)
                    {
                        double? li_16 = Math.Round(ai_4 - ad_8 * Symbol.PipSize, Symbol.Digits);
                        if (position.TakeProfit != li_16)
                            ModifyPosition(position, position.StopLoss, li_16);
                    }
                }
            }
        }

        private int TotalOpenPositions(TradeType tradeType)
        {
            return Positions.FindAll(Label, Symbol, tradeType).Length;
        }

        private double AveragePrice(TradeType tradeType)
        {
            double totalPositionValue = 0;
            long totalVolume = 0;

            foreach (var position in Positions.FindAll(Label, Symbol, tradeType))
            {
                totalPositionValue += position.EntryPrice * position.Volume;
                totalVolume += position.Volume;
            }

            if (totalPositionValue > 0 && totalVolume > 0)
                return Math.Round(totalPositionValue / totalVolume, Symbol.Digits);

            return 0;
        }

        private double GetMinEntryPrice(TradeType tradeType)
        {
            var positions = Positions.FindAll(Label, Symbol, tradeType);

            if (positions.Length == 0)
                return 0;

            return positions.Min(i => i.EntryPrice);
        }

        private double GetMaxEntryPrice(TradeType tradeType)
        {
            var positions = Positions.FindAll(Label, Symbol, tradeType);

            if (positions.Length == 0)
                return 0;

            return positions.Max(i => i.EntryPrice);
        }

        private double GetFirstEntryPrice(TradeType tradeType)
        {
            var lastPosition = Positions.FindAll(Label, Symbol, tradeType).OrderBy(i => i.Id).FirstOrDefault();

            if (lastPosition == null)
                return 0;

            return lastPosition.EntryPrice;
        }

        private long GetFirstVolume(TradeType tradeType)
        {
            var lastPosition = Positions.FindAll(Label, Symbol, tradeType).OrderBy(i => i.Id).FirstOrDefault();

            if (lastPosition == null)
                return 0;

            return lastPosition.Volume;
        }

        private long PositionVolume(TradeType tradeType)
        {
            return Positions.FindAll(Label, Symbol, tradeType).Sum(i => i.Volume);
        }

        private int GetTotalOperationOnPrice(TradeType tradeType, double price)
        {
            var positions = Positions.FindAll(Label, Symbol, tradeType);

            if (tradeType == TradeType.Buy)
                return positions.Count(i => Math.Round(i.EntryPrice, Symbol.Digits) <= Math.Round(price, Symbol.Digits));

            return positions.Count(i => Math.Round(i.EntryPrice, Symbol.Digits) >= Math.Round(price, Symbol.Digits));
        }

        private long CalculateVolume(TradeType tradeType)
        {
            double firstEntryPrice = GetFirstEntryPrice(tradeType);
            long firstVolume = GetFirstVolume(tradeType);
            int totalOperation = GetTotalOperationOnPrice(tradeType, firstEntryPrice);

            return Symbol.NormalizeVolume(firstVolume * Math.Pow(VolumeExponent, totalOperation));
        }
    }
}
