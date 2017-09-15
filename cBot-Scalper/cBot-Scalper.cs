using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class NewRobot : Robot
    {
        [Parameter("Vol", DefaultValue = 10000)]  
        public int Vol { get; set; }

        [Parameter("SL", DefaultValue = 200)]
        public int SL { get; set; }

        [Parameter("TP", DefaultValue = 200)]
        public int TP { get; set; }

        [Parameter("Tral_Start", DefaultValue = 150)]
        public int Tral_Start { get; set; }

        [Parameter("Tral_Stop", DefaultValue = 100)]
        public int Tral_Stop { get; set; }

        [Parameter("MA_price")]
        public DataSeries MA_price { get; set; }

        [Parameter("MA_period", DefaultValue = 18)]
        public int MA_period { get; set; }

        [Parameter("MAType")]
        public MovingAverageType MAType { get; set; }

        [Parameter("WPR_period", DefaultValue = 11)]
        public int WPR_period { get; set; }

        [Parameter("Period_K1", DefaultValue = 14)]
        public int Period_K1 { get; set; }

        [Parameter("Period_D1", DefaultValue = 5)]
        public int Period_D1 { get; set; }

        [Parameter("Slowing1", DefaultValue = 5)]
        public int Slowing1 { get; set; }

        [Parameter("St_Ma_Type1")]
        public MovingAverageType St_Ma_Type1 { get; set; }

        [Parameter("Period_K2", DefaultValue = 4)]
        public int Period_K2 { get; set; }

        [Parameter("Period_D2", DefaultValue = 3)]
        public int Period_D2 { get; set; }

        [Parameter("Slowing2", DefaultValue = 3)]
        public int Slowing2 { get; set; }

        [Parameter("St_Ma_Type2")]
        public MovingAverageType St_Ma_Type2 { get; set; }


        private MovingAverage MA;
        private WilliamsPctR WPR;
        private StochasticOscillator St1;
        private StochasticOscillator St2;
        private Position position1;
        private int a;

        protected override void OnStart()
        {
            Print("Welcome to the world of infinite financial possibilities!");
            MA = Indicators.MovingAverage(MA_price, MA_period, MAType);
            WPR = Indicators.WilliamsPctR(WPR_period);
            St1 = Indicators.StochasticOscillator(Period_K1, Period_D1, Slowing1, St_Ma_Type1);
            St2 = Indicators.StochasticOscillator(Period_K2, Period_D2, Slowing2, St_Ma_Type2);
        }

        protected override void OnPositionOpened(Position openedPosition)
        {
            position1 = openedPosition;

            if (position1.TradeType == TradeType.Buy)
            {
                Trade.ModifyPosition(openedPosition, position1.EntryPrice - SL * Symbol.PointSize, position1.EntryPrice + TP * Symbol.PointSize);
                Print("StopLoss and TakeProfit were successfully established");
            }

            if (position1.TradeType == TradeType.Sell)
            {
                Trade.ModifyPosition(openedPosition, position1.EntryPrice + SL * Symbol.PointSize, position1.EntryPrice - TP * Symbol.PointSize);
                Print("StopLoss and TakeProfit were successfully established");
            }
        }

        protected override void OnTick()
        {

            int bars = MarketSeries.Close.Count - 1;
            double cl1 = MarketSeries.Close[bars - 1];
            double cl2 = MarketSeries.Close[bars - 2];

            double MA1 = MA.Result[MA.Result.Count - 2];
            double MA2 = MA.Result[MA.Result.Count - 3];
            double WPR1 = WPR.Result[WPR.Result.Count - 2];
            double St11 = St1.PercentK[St1.PercentK.Count - 2];
            double St21 = St1.PercentD[St1.PercentD.Count - 2];
            double St12 = St2.PercentD[St2.PercentD.Count - 2];

            double Bid = Symbol.Bid;
            double Ask = Symbol.Ask;
            double Point = Symbol.PointSize;

            if (Trade.IsExecuting)
                return;

            if (WPR1 > -50 && cl1 > MA1 && cl2 <= MA2 && St11 > St21 && St12 > 20 && a == 0)
            {
                Trade.CreateBuyMarketOrder(Symbol, Vol);
                Print("Trade BUY was successfully open");
                a = 1;
            }

            if (WPR1 < -50 && cl1 < MA1 && cl2 >= MA2 && St11 < St21 && St12 < 80 && a == 0)
            {
                Trade.CreateSellMarketOrder(Symbol, Vol);
                Print("Trade SELL was successfully open");
                a = 1;
            }

            foreach (var position in Account.Positions)
            {
                if (position.SymbolCode == Symbol.Code)
                {

                    if (position.TradeType == TradeType.Buy)
                    {
                        if (Bid - position.EntryPrice >= Tral_Start * Point)
                            if (Bid - Tral_Stop * Point >= position.StopLoss)
                                Trade.ModifyPosition(position, Bid - Tral_Stop * Point, position.TakeProfit);
                    }

                    if (position.TradeType == TradeType.Sell)
                    {
                        if (position.EntryPrice - Ask >= Tral_Start * Point)
                            if (Ask + Tral_Stop * Point <= position.StopLoss)
                                Trade.ModifyPosition(position, Ask + Tral_Stop * Point, position.TakeProfit);
                    }
                }
            }

        }

        protected override void OnPositionClosed(Position pos)
        {
            a = 0;
        }

        protected override void OnStop()
        {
            Print("Successful day!");
        }

    }
}
