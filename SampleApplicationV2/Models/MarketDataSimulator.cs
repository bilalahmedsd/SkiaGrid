using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace SampleApplicationV2.Models
{
    public class MarketDataSimulator
    {
        private static readonly Random Rand = new();
        private static readonly string[] Symbols = { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "JPM", "BAC", "GS", "V", "MA", "WMT", "JNJ", "PFE", "UNH", "XOM", "CVX", "COP", "NEE" };
        private static readonly string[] Exchanges = { "NYSE", "NASDAQ", "ARCA", "BATS", "IEX", "EDGX" };
        private static readonly string[] Sectors = { "Technology", "Finance", "Healthcare", "Energy", "Consumer" };
        private static readonly string[] Accounts = { "ACCT-001", "ACCT-002", "ACCT-003", "ACCT-004" };
        private static readonly string[] OrderTypes = { "LMT", "MKT", "STP", "STPLMT" };
        private static readonly string[] Sides = { "BUY", "SELL", "SHORT" };
        private static readonly string[] Statuses = { "Open", "Filled", "Part Fill", "Cancelled", "Pending Cancel", "Rejected" };
        private static readonly string[] Conditions = { "", "Regular", "FormT", "OddLot", "Intermarket", "Derivatively" };
        /// <summary>
        /// Single-letter SIP participant codes as a real tape prints them (P = NYSE Arca, Q = Nasdaq,
        /// D = FINRA ADF, K = EDGX, Z = BZX, T = Nasdaq TRF, O = NYSE American, B = Nasdaq BX). Used by
        /// the Time &amp; Sales tape, which shows a narrow "Mkt" column; the verbose <see cref="Exchanges"/>
        /// names stay in use by the position / order / execution generators.
        /// </summary>
        private static readonly string[] TapeMarkets = { "P", "Q", "D", "K", "Z", "T", "O", "B" };
        /// <summary>Tape text colors: at-ask green, at-bid red, in-between amber.</summary>
        private const string TapeAtAskColor = "#40FF40";
        private const string TapeAtBidColor = "#FF4848";
        private const string TapeMidColor   = "#FFA953";
        private static readonly string[] Routes = { "SMART", "DIRECT", "ARCA", "NYSE", "BATS" };
        private static readonly string[] Expiries = { "Apr 18", "Apr 25", "May 02", "May 16", "Jun 20", "Jul 18", "Sep 19", "Dec 19" };

        private static readonly Dictionary<string, double> BasePrices = new();

        static MarketDataSimulator()
        {
            var prices = new[] { 178.50, 420.30, 175.20, 185.40, 245.80, 880.50, 510.20, 198.30, 38.50, 425.10, 280.40, 460.20, 165.30, 155.80, 28.40, 540.20, 108.30, 155.60, 118.40, 78.50 };
            for (int i = 0; i < Symbols.Length; i++)
                BasePrices[Symbols[i]] = prices[i];
        }

        public static double GetBasePrice(string symbol) => BasePrices.GetValueOrDefault(symbol, 100.0);

        public static double Tick(double price, double maxPctMove = 0.005)
        {
            var move = price * maxPctMove * (Rand.NextDouble() * 2 - 1);
            return Math.Round(price + move, 2);
        }

        public static List<QuoteItem> GenerateWatchlist()
        {
            var list = new List<QuoteItem>();
            foreach (var sym in Symbols)
            {
                var basePrice = BasePrices[sym];
                var last = Tick(basePrice, 0.02);
                var change = last - basePrice;
                list.Add(new QuoteItem
                {
                    Symbol = sym,
                    Last = last,
                    Change = Math.Round(change, 2),
                    ChangePct = Math.Round(change / basePrice * 100, 2),
                    Bid = Math.Round(last - Rand.NextDouble() * 0.1, 2),
                    Ask = Math.Round(last + Rand.NextDouble() * 0.1, 2),
                    BidSize = Rand.Next(1, 50) * 100,
                    AskSize = Rand.Next(1, 50) * 100,
                    High = Math.Round(last + Rand.NextDouble() * 3, 2),
                    Low = Math.Round(last - Rand.NextDouble() * 3, 2),
                    Open = Math.Round(basePrice + (Rand.NextDouble() - 0.5) * 2, 2),
                    Close = basePrice,
                    Volume = Rand.Next(100000, 50000000),
                    Exchange = Exchanges[Rand.Next(Exchanges.Length)],
                    Sector = Sectors[Rand.Next(Sectors.Length)]
                });
            }
            return list;
        }

        public static void UpdateQuotes(ObservableCollection<QuoteItem> quotes)
        {
            foreach (var q in quotes)
            {
                var oldLast = q.Last;
                q.Last = Tick(q.Last);
                q.Change = Math.Round(q.Last - q.Close, 2);
                q.ChangePct = Math.Round(q.Change / q.Close * 100, 2);
                q.Bid = Math.Round(q.Last - Rand.NextDouble() * 0.1, 2);
                q.Ask = Math.Round(q.Last + Rand.NextDouble() * 0.1, 2);
                q.BidSize = Rand.Next(1, 50) * 100;
                q.AskSize = Rand.Next(1, 50) * 100;
                if (q.Last > q.High) q.High = q.Last;
                if (q.Last < q.Low) q.Low = q.Last;
                q.Volume += Rand.Next(100, 10000);
                q.LastColor = q.Last > oldLast ? "#00FF00" : q.Last < oldLast ? "#FF0000" : "#FFFFFF";
            }
        }

        public static TradeRecord GenerateTrade()
        {
            var sym = Symbols[Rand.Next(Symbols.Length)];
            var basePrice = BasePrices[sym];
            var price = Tick(basePrice, 0.01);
            var bid = Math.Round(price - Rand.NextDouble() * 0.05, 2);
            var ask = Math.Round(price + Rand.NextDouble() * 0.05, 2);
            var side = price >= ask ? "Ask" : price <= bid ? "Bid" : "Mid";

            return new TradeRecord
            {
                Symbol = sym,
                Time = DateTime.Now,
                Price = price,
                Size = Rand.Next(1, 100) * 100,
                Market = TapeMarkets[Rand.Next(TapeMarkets.Length)],
                Condition = Conditions[Rand.Next(Conditions.Length)],
                Bid = bid,
                Ask = ask,
                Side = side,
                BidAskColor = side == "Ask" ? TapeAtAskColor : side == "Bid" ? TapeAtBidColor : TapeMidColor
            };
        }

        private static readonly string[] SpreadTypes = { "Vertical", "Butterfly", "Iron Condor", "Calendar", "Straddle", "Strangle" };

        public static List<OrderRecord> GenerateOrders(int count = 30)
        {
            var list = new List<OrderRecord>();
            for (int i = 0; i < count; i++)
            {
                // Every 5th order is a spread/complex order
                if (i % 5 == 0)
                {
                    list.Add(GenerateSpreadOrder());
                }
                else
                {
                    list.Add(GenerateSimpleOrder());
                }
            }
            return list;
        }

        private static OrderRecord GenerateSimpleOrder()
        {
            var sym = Symbols[Rand.Next(Symbols.Length)];
            var side = Sides[Rand.Next(Sides.Length)];
            var qty = Rand.Next(1, 50) * 100;
            var status = Statuses[Rand.Next(Statuses.Length)];
            var filled = status == "Filled" ? qty : status == "Part Fill" ? Rand.Next(1, qty) : 0;
            var price = Tick(BasePrices[sym], 0.01);

            return new OrderRecord
            {
                OrderId = $"ORD-{Rand.Next(10000, 99999)}",
                Symbol = sym,
                Side = side,
                Type = OrderTypes[Rand.Next(OrderTypes.Length)],
                Price = price,
                Quantity = qty,
                Filled = filled,
                Remaining = qty - filled,
                Status = status,
                Account = Accounts[Rand.Next(Accounts.Length)],
                Time = DateTime.Now.AddMinutes(-Rand.Next(0, 480)),
                Route = Routes[Rand.Next(Routes.Length)],
                AvgPrice = filled > 0 ? Tick(price, 0.002) : 0,
                Instrument = new InstrumentInfo
                {
                    Exchange = Exchanges[Rand.Next(Exchanges.Length)],
                    Currency = Rand.Next(4) == 0 ? "EUR" : "USD"
                }
            };
        }

        private static OrderRecord GenerateSpreadOrder()
        {
            var sym = Symbols[Rand.Next(Symbols.Length)];
            var basePrice = BasePrices[sym];
            var spreadType = SpreadTypes[Rand.Next(SpreadTypes.Length)];
            var acct = Accounts[Rand.Next(Accounts.Length)];
            var route = Routes[Rand.Next(Routes.Length)];
            var time = DateTime.Now.AddMinutes(-Rand.Next(0, 480));
            var status = Statuses[Rand.Next(3)]; // Open, Filled, Part Fill only for spreads

            int legCount = spreadType switch
            {
                "Vertical" => 2,
                "Straddle" => 2,
                "Strangle" => 2,
                "Calendar" => 2,
                "Butterfly" => 3,
                "Iron Condor" => 4,
                _ => 2
            };

            var legs = new List<OrderRecord>();
            int totalQty = 0;
            int totalFilled = 0;
            double netPrice = 0;

            for (int leg = 0; leg < legCount; leg++)
            {
                var legSide = (leg % 2 == 0) ? "BUY" : "SELL";
                var legQty = Rand.Next(1, 20) * 100;
                var strike = Math.Round(basePrice + (leg - legCount / 2.0) * 5, 2);
                var legPrice = Math.Round(Math.Max(0.5, Rand.NextDouble() * 10 + 1), 2);
                var legFilled = status == "Filled" ? legQty : status == "Part Fill" ? Rand.Next(1, legQty) : 0;

                var legOrder = new OrderRecord
                {
                    OrderId = $"LEG-{Rand.Next(10000, 99999)}",
                    Symbol = $"{sym} {strike:F0}{(leg % 2 == 0 ? "C" : "P")}",
                    Side = legSide,
                    Type = "LMT",
                    Price = legPrice,
                    Quantity = legQty,
                    Filled = legFilled,
                    Remaining = legQty - legFilled,
                    Status = legFilled >= legQty ? "Filled" : legFilled > 0 ? "Part Fill" : "Open",
                    Account = acct,
                    Time = time,
                    Route = route,
                    AvgPrice = legFilled > 0 ? Tick(legPrice, 0.002) : 0,
                    Instrument = new InstrumentInfo
                    {
                        Exchange = Exchanges[Rand.Next(Exchanges.Length)],
                        Currency = "USD"
                    }
                };

                legs.Add(legOrder);
                totalQty += legQty;
                totalFilled += legFilled;
                netPrice += (legSide == "BUY" ? -legPrice : legPrice) * legQty;
            }

            return new OrderRecord
            {
                OrderId = $"SPD-{Rand.Next(10000, 99999)}",
                Symbol = sym,
                Side = "SPREAD",
                Type = spreadType,
                Price = Math.Round(Math.Abs(netPrice / totalQty), 2),
                Quantity = totalQty,
                Filled = totalFilled,
                Remaining = totalQty - totalFilled,
                Status = status,
                Account = acct,
                Time = time,
                Route = route,
                AvgPrice = totalFilled > 0 ? Math.Round(Math.Abs(netPrice / totalQty), 2) : 0,
                Legs = legs,
                Instrument = new InstrumentInfo
                {
                    Exchange = Exchanges[Rand.Next(Exchanges.Length)],
                    Currency = "USD"
                }
            };
        }

        public static void SimulateOrderUpdates(ObservableCollection<OrderRecord> orders)
        {
            var openOrders = orders.Where(o => o.Status == "Open" || o.Status == "Part Fill").ToList();
            if (openOrders.Count == 0) return;

            var order = openOrders[Rand.Next(openOrders.Count)];
            if (Rand.NextDouble() < 0.3)
            {
                var fillQty = Math.Min(Rand.Next(100, 500), order.Remaining);
                order.Filled += fillQty;
                order.Remaining -= fillQty;
                order.AvgPrice = Tick(order.Price, 0.002);
                order.Status = order.Remaining <= 0 ? "Filled" : "Part Fill";
            }
        }

        public static List<PositionItem> GeneratePositions(int count = 25)
        {
            var list = new List<PositionItem>();
            var usedSymbols = new HashSet<string>();

            for (int i = 0; i < Math.Min(count, Symbols.Length * Accounts.Length); i++)
            {
                var acct = Accounts[i % Accounts.Length];
                var sym = Symbols[i / Accounts.Length % Symbols.Length];
                var key = $"{acct}_{sym}";
                if (usedSymbols.Contains(key)) continue;
                usedSymbols.Add(key);

                var basePrice = BasePrices[sym];
                var pos = (Rand.Next(2) == 0 ? 1 : -1) * Rand.Next(1, 100) * 100;
                var avgCost = Tick(basePrice, 0.05);
                var lastPrice = Tick(basePrice, 0.02);

                list.Add(new PositionItem
                {
                    Account = acct,
                    Symbol = sym,
                    Position = pos,
                    AvgCost = avgCost,
                    LastPrice = lastPrice,
                    RealizedPnl = Math.Round((Rand.NextDouble() - 0.4) * 5000, 2),
                    Currency = "USD",
                    AssetClass = Rand.NextDouble() > 0.3 ? "Stock" : "ETF",
                    Exchange = Exchanges[Rand.Next(Exchanges.Length)]
                });
            }
            return list;
        }

        public static void UpdatePositionPrices(ObservableCollection<PositionItem> positions)
        {
            foreach (var p in positions)
            {
                p.LastPrice = Tick(p.LastPrice);
            }
        }

        public static List<OptionItem> GenerateOptionsChain(string symbol = "AAPL", double underlyingPrice = 178.50)
        {
            var list = new List<OptionItem>();
            var strikes = Enumerable.Range(-10, 21).Select(i => Math.Round(underlyingPrice + i * 2.5, 2)).ToArray();
            var expiry = Expiries[0];

            foreach (var strike in strikes)
            {
                // Call
                var callItm = underlyingPrice > strike;
                var callIntrinsic = callItm ? underlyingPrice - strike : 0;
                var callTimeVal = Rand.NextDouble() * 5 + 0.5;
                var callPrice = Math.Round(callIntrinsic + callTimeVal, 2);

                list.Add(new OptionItem
                {
                    Symbol = symbol,
                    Strike = strike,
                    Expiry = expiry,
                    OptionType = "Call",
                    Bid = Math.Round(callPrice - Rand.NextDouble() * 0.2, 2),
                    Ask = Math.Round(callPrice + Rand.NextDouble() * 0.2, 2),
                    Last = callPrice,
                    Volume = Rand.Next(0, 5000),
                    OpenInterest = Rand.Next(100, 50000),
                    ImpliedVol = Math.Round(0.15 + Rand.NextDouble() * 0.3, 4),
                    Delta = Math.Round(callItm ? 0.5 + Rand.NextDouble() * 0.5 : Rand.NextDouble() * 0.5, 4),
                    Gamma = Math.Round(Rand.NextDouble() * 0.05, 4),
                    Theta = -Math.Round(Rand.NextDouble() * 0.5, 4),
                    Vega = Math.Round(Rand.NextDouble() * 0.3, 4),
                    IsITM = callItm,
                    UnderlyingPrice = underlyingPrice
                });

                // Put
                var putItm = underlyingPrice < strike;
                var putIntrinsic = putItm ? strike - underlyingPrice : 0;
                var putTimeVal = Rand.NextDouble() * 5 + 0.5;
                var putPrice = Math.Round(putIntrinsic + putTimeVal, 2);

                list.Add(new OptionItem
                {
                    Symbol = symbol,
                    Strike = strike,
                    Expiry = expiry,
                    OptionType = "Put",
                    Bid = Math.Round(putPrice - Rand.NextDouble() * 0.2, 2),
                    Ask = Math.Round(putPrice + Rand.NextDouble() * 0.2, 2),
                    Last = putPrice,
                    Volume = Rand.Next(0, 5000),
                    OpenInterest = Rand.Next(100, 50000),
                    ImpliedVol = Math.Round(0.15 + Rand.NextDouble() * 0.3, 4),
                    Delta = -Math.Round(putItm ? 0.5 + Rand.NextDouble() * 0.5 : Rand.NextDouble() * 0.5, 4),
                    Gamma = Math.Round(Rand.NextDouble() * 0.05, 4),
                    Theta = -Math.Round(Rand.NextDouble() * 0.5, 4),
                    Vega = Math.Round(Rand.NextDouble() * 0.3, 4),
                    IsITM = putItm,
                    UnderlyingPrice = underlyingPrice
                });
            }
            return list;
        }

        public static void UpdateOptionsChain(ObservableCollection<OptionItem> options)
        {
            foreach (var opt in options)
            {
                var oldLast = opt.Last;
                opt.Last = Math.Round(Math.Max(0.01, Tick(opt.Last, 0.02)), 2);
                opt.Bid = Math.Round(Math.Max(0.01, opt.Last - Rand.NextDouble() * 0.2), 2);
                opt.Ask = Math.Round(opt.Last + Rand.NextDouble() * 0.2, 2);
                opt.Volume += Rand.Next(0, 100);
                opt.ImpliedVol = Math.Round(Math.Max(0.05, Tick(opt.ImpliedVol, 0.01)), 4);
                opt.TrendColor = opt.Last > oldLast ? "#00FF00" : opt.Last < oldLast ? "#FF0000" : "#FFFFFF";
            }
        }
    }
}
