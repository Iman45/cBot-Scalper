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
        [Parameter("Buy Enabled", DefaultValue = true)]
        public bool BuyEnabled { get; set; }

        [Parameter("Sell Enabled", DefaultValue = true)]
        public bool SellEnabled { get; set; }

        [Parameter("Pip Step", DefaultValue = 1, MinValue = 0.01)]
        public double PipStep { get; set; }

        [Parameter("Lots", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Lots { get; set; }

        [Parameter("Fix Stop Loss Pips", DefaultValue = 100, MinValue = 0)]
        public double FixStopLossPips { get; set; }

        [Parameter("Volume Exponent", DefaultValue = 1, MinValue = 0.1, MaxValue = 5.0)]
        public double VolumeExponent { get; set; }

        [Parameter("Max Spread", DefaultValue = 3, MinValue = 0)]
        public double MaxSpread { get; set; }

        [Parameter("Take Profit Average Pips", DefaultValue = 21, MinValue = 0.01)]
        public double TakeProfitAveragePips { get; set; }

        private string Label = "cls";
        private DateTime buyLastOpenTime;
        private DateTime sellLastOpenTime;
        private double spreedValue;
        private bool stopped = false;

        protected override void OnTick()
        {
            try
            {
                spreedValue = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;

                if (TotalOpenPositions(TradeType.Buy) > 0)
                    SetBuyTakeProfit(AveragePrice(TradeType.Buy), TakeProfitAveragePips);

                if (TotalOpenPositions(TradeType.Sell) > 0)
                    SetSellTakeProfit(AveragePrice(TradeType.Sell), TakeProfitAveragePips);

                if (spreedValue <= MaxSpread && !stopped)
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

                Print("Openning stopped because: not enough money");
            }

            Print("Error: ", error);
        }

        private void OpenPosition()
        {
            if (BuyEnabled && TotalOpenPositions(TradeType.Buy) == 0 && MarketSeries.Close.Last(1) > MarketSeries.Close.Last(2))
            {
                var success = SendOrder(TradeType.Buy, Symbol.QuantityToVolume(Lots));

                if (success)
                    buyLastOpenTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First BUY openning error at: {0}, Error Type: {1}", Symbol.Ask, LastResult.Error);
            }

            if (SellEnabled && TotalOpenPositions(TradeType.Sell) == 0 && MarketSeries.Close.Last(2) > MarketSeries.Close.Last(1))
            {
                var success = SendOrder(TradeType.Sell, Symbol.QuantityToVolume(Lots));

                if (success)
                    sellLastOpenTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First SELL openning error at: {0}, Error Type: {1}", Symbol.Bid, LastResult.Error);
            }

            MakeAveragePrice();
        }

        private void MakeAveragePrice()
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
                        Print("Next BUY openning error at: {0}, Error Type: {1}", Symbol.Ask, LastResult.Error);
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
                        Print("Next SELL openning error at: {0}, Error Type: {1}", Symbol.Bid, LastResult.Error);
                }
            }
        }

        private bool SendOrder(TradeType tradeType, long volume)
        {
            if (volume <= 0)
            {
                Print("Volume calculation error: Calculated Volume is: ", volume);

                return false;
            }

            TradeResult result = ExecuteMarketOrder(tradeType, Symbol, volume, Label, FixStopLossPips, 0, 0, "smart_grid");

            if (!result.IsSuccessful)
            {
                Print("Openning Error: ", result.Error);

                return false;
            }

            Print("Opened at: ", result.Position.EntryPrice);

            return true;
        }

        private void SetBuyTakeProfit(double averagePrice, double takeProfitAveragePips)
        {
            foreach (var position in Positions.FindAll(Label, Symbol, TradeType.Buy))
            {
                double? takeProfit = Math.Round(averagePrice + takeProfitAveragePips * Symbol.PipSize, Symbol.Digits);

                if (position.TakeProfit != takeProfit)
                {
                    TradeResult result = ModifyPosition(position, position.StopLoss, takeProfit);

                    if (!result.IsSuccessful)
                        Print("Modify Error: ", result.Error);
                }
            }
        }

        private void SetSellTakeProfit(double averagePrice, double takeProfitAveragePips)
        {
            foreach (var position in Positions.FindAll(Label, Symbol, TradeType.Sell))
            {
                double? takeProfit = Math.Round(averagePrice - takeProfitAveragePips * Symbol.PipSize, Symbol.Digits);

                if (position.TakeProfit != takeProfit)
                {
                    TradeResult result = ModifyPosition(position, position.StopLoss, takeProfit);

                    if (!result.IsSuccessful)
                        Print("Modify Error: ", result.Error);
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

        private int GetTotalOperationBelowPrice(TradeType tradeType, double price)
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
            int totalOperation = GetTotalOperationBelowPrice(tradeType, firstEntryPrice);


            var volume = Symbol.NormalizeVolume(firstVolume * Math.Pow(VolumeExponent, totalOperation == 0 ? 1 : totalOperation));

            if (volume > Symbol.VolumeMax)
                return Symbol.VolumeMax;

            if (volume < Symbol.VolumeMin)
                return Symbol.VolumeMin;

            return volume;
        }
    }
}
