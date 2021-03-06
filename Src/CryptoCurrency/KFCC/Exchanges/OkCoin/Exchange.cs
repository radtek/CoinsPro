﻿using KFCC.ExchangeInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonLab;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;

namespace KFCC.Exchanges.EOkCoin
{
    public class OkCoinExchange : IExchanges
    {
        private static string _secret;
        private static string _key;
        private static string _uid;
        private static string _username;
        private Proxy _proxy = null;
        static private Dictionary<string, KFCC.ExchangeInterface.SubscribeInterface> _subscribedtradingpairs = null;
        static private string ApiUrl = @"https://www.okex.com/api/v1/";

        public string Name { get { return "OkCoin"; } }
        public string ExchangeUrl { get { return "www.okex.com"; } }
        public string Remark { get { return "okcoin(okex) exchange remark"; } }
        public string Secret { get { return _secret; } set { _secret = value; } }
        public string Key { get { return _key; } set { _key = value; } }
        public string UID { get { return _uid; } set { _uid = value; } }
        public string UserName { get { return _username; } set { _username = value; } }

        public Dictionary<string, KFCC.ExchangeInterface.SubscribeInterface> SubscribedTradingPairs { get { return _subscribedtradingpairs; } }
        public Fee eFee { get; set; }
        public Proxy proxy { get { return _proxy; } set { _proxy = value; } }
        private Account _account;
        public Account Account { get { return _account; } set { _account = value; } }
        //public bool SportWSS { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        //public bool SportThirdPartWSS { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event ExchangeEventWarper.TickerEventHander TickerEvent;
        public event ExchangeEventWarper.DepthEventHander DepthEvent;
        public event ExchangeEventWarper.TradeEventHander TradeEvent;
        public event ExchangeEventWarper.SubscribedEventHander SubscribedEvent;

        public OkCoinExchange(string key, string secret, string uid, string username)
        {
            _key = key;
            _secret = secret;
            _uid = uid;
            _username = username;
        }
        public OkCoinExchange()
        {
          
        }
       
        public void SetupExchage(string key, string secret, string uid, string username)
        {
            _key = key;
            _secret = secret;
            _uid = uid;
            _username = username;
        }
        public void SetupFee(string maker, string taker)
        {
            eFee = new Fee();
            eFee.MakerFee = Convert.ToDouble(maker) / 100;
            eFee.TakerFee = Convert.ToDouble(taker) / 100;
        }
        public bool Subscribe(CommonLab.TradePair tp, SubscribeTypes st)
        {
            //throw new NotImplementedException();
            //订阅 
            string tradingpairs = GetLocalTradingPairString(tp, st);
            if (st == SubscribeTypes.WSS)
            {
                if (_subscribedtradingpairs == null)
                {
                    _subscribedtradingpairs = new Dictionary<string, KFCC.ExchangeInterface.SubscribeInterface>();

                }
                if (_subscribedtradingpairs.ContainsKey(tradingpairs))
                {
                    //有这个订阅
                }
                else
                {
                    string raw;
                    Ticker t = GetTicker(GetLocalTradingPairString(tp), out raw);
                    Depth d = GetDepth(GetLocalTradingPairString(tp), out raw);
                    _subscribedtradingpairs.Add(tradingpairs, new WssHelper(tp, t, d));
                    _subscribedtradingpairs[tradingpairs].TradeInfoEvent += OkCoinExchange_TradeInfoEvent;
                }
            }
            else if (st == SubscribeTypes.RESTAPI)
            {
                if (_subscribedtradingpairs == null)
                {
                    _subscribedtradingpairs = new Dictionary<string, KFCC.ExchangeInterface.SubscribeInterface>();

                }
                if (_subscribedtradingpairs.ContainsKey(tradingpairs))
                {
                    //有这个订阅
                }
                else
                {
                    string raw;
                    Ticker t = GetTicker(GetLocalTradingPairString(tp), out raw);
                    Depth d = GetDepth(GetLocalTradingPairString(tp), out raw);
                    _subscribedtradingpairs.Add(tradingpairs, new RESTHelper(tp, t, d));
                    //_subscribedtradingpairs[tradingpairs].TradeInfoEvent += OkCoinExchange_TradeInfoEvent;
                }
            }
            if (SubscribedEvent != null)
                SubscribedEvent(this, st, tp);
            return true;
        }

        private void OkCoinExchange_TradeInfoEvent(TradingInfo ti, TradeEventType tt)
        {
            if (TickerEvent != null && tt == TradeEventType.TICKER)
            {
                TickerEvent(this, ti.t, (CommonLab.EventTypes)ti.type, ti.tp);
            }
            if (DepthEvent != null && tt == TradeEventType.ORDERS)
            {
                DepthEvent(this, ti.d, (CommonLab.EventTypes)ti.type, ti.tp);
            }
            if (TradeEvent != null && tt == TradeEventType.TRADE)
            {
                TradeEvent(this, ti.trade, (CommonLab.EventTypes)ti.type, ti.tp);
                
            }
        }

        public Ticker GetTicker(string tradingpair, out string rawresponse)
        {
            DateTime st = DateTime.Now;
            //throw new NotImplementedException();
            string url = GetPublicApiURL(tradingpair, "ticker"+ ".do?symbol=" );
            rawresponse = CommonLab.Utility.GetHttpContent(url, "GET", "", _proxy);
            CommonLab.Ticker t = new Ticker();
            JObject obj = JObject.Parse(rawresponse);
            try
            {
                JObject ticker = JObject.Parse(obj["ticker"].ToString());
                t.High = Convert.ToDouble(ticker["high"].ToString());
                t.Low = Convert.ToDouble(ticker["low"].ToString());
                t.Last = Convert.ToDouble(ticker["last"].ToString());
                t.Sell = Convert.ToDouble(ticker["sell"].ToString());
                t.Buy = Convert.ToDouble(ticker["buy"].ToString());
                t.Volume = Convert.ToDouble(ticker["vol"].ToString());
                t.Open = 0;// Convert.ToDouble(ticker["open"].ToString());
                t.ExchangeTimeStamp = Convert.ToDouble(obj["date"].ToString());
                t.LocalServerTimeStamp = CommonLab.TimerHelper.GetTimeStampMilliSeconds(DateTime.Now);
                t.Delay = (DateTime.Now - st).TotalMilliseconds;
                UpdateTicker(tradingpair, t);
            }
            catch
            {

            }
            return t;
        }

        public Depth GetDepth(string tradingpair, out string rawresponse)
        {
            DateTime st = DateTime.Now;
            string url = GetPublicApiURL(tradingpair, "depth" + ".do?symbol=");
            rawresponse = CommonLab.Utility.GetHttpContent(url, "GET", "", _proxy);
            CommonLab.Depth d = new Depth();
            d.Asks = new List<MarketOrder>();
            d.Bids = new List<MarketOrder>();
            JObject obj = JObject.Parse(rawresponse);
            JArray jasks = JArray.Parse(obj["asks"].ToString());
            for (int i = 0; i < jasks.Count; i++)
            {
                MarketOrder m = new MarketOrder();
                m.Price = Convert.ToDouble(JArray.Parse(jasks[i].ToString())[0]);
                m.Amount = Convert.ToDouble(JArray.Parse(jasks[i].ToString())[1]);
                d.AddNewAsk(m);
            }

            JArray jbids = JArray.Parse(obj["bids"].ToString());
            for (int i = 0; i < jbids.Count; i++)
            {
                MarketOrder m = new MarketOrder();
                m.Price = Convert.ToDouble(JArray.Parse(jbids[i].ToString())[0]);
                m.Amount = Convert.ToDouble(JArray.Parse(jbids[i].ToString())[1]);
                d.AddNewBid(m);
            }
            d.ExchangeTimeStamp = 0;// Convert.ToDouble(obj["timestamp"].ToString());
            d.LocalServerTimeStamp = CommonLab.TimerHelper.GetTimeStampMilliSeconds(DateTime.Now);
            d.Delay = (DateTime.Now - st).TotalMilliseconds;
            UpdateDepth(tradingpair, d);
            return d;
        }

        public Trade[] GetTrades(string tradepair, out string rawresponse,string since="0")
        {
            List<Trade> trades = new List<CommonLab.Trade>();
            string url = GetPublicApiURL(tradepair, "trades" + ".do?symbol=");
            if (since != "0")
            {
                url += "&since=" + since;
            }
            rawresponse = CommonLab.Utility.GetHttpContent(url, "GET", "", _proxy);
            
            JArray obj = JArray.Parse(rawresponse);
            for (int i = 0; i < obj.Count; i++)
            {
                JObject trade = JObject.Parse(obj[i].ToString());
                Trade t = new CommonLab.Trade();
                t.TradeID = trade["tid"].ToString();
                t.Amount = Convert.ToDouble(trade["amount"].ToString());
                t.Price = Convert.ToDouble(trade["price"].ToString());
                t.Type = CommonLab.Trade.GetType(trade["type"].ToString());
                t.ExchangeTimeStamp = Convert.ToDouble(trade["date_ms"].ToString())/1000; //时间戳 交易所返回的
                t.LocalServerTimeStamp = CommonLab.TimerHelper.GetTimeStampMilliSeconds(DateTime.Now); //本地时间戳
                t.BuyOrderID = "";//成交的交易号
                t.SellOrderID = "";//成交的交易号
                trades.Add(t);
            }
            if (trades.Count > 0)
                return trades.ToArray();
            return null;
        }
        /// <summary>
        /// 更新深度数据
        /// </summary>
        /// <param name="d"></param>
        protected void UpdateDepth(string tradingpair, Depth d)
        {
            if (SubscribedTradingPairs != null)
                if (SubscribedTradingPairs.ContainsKey(tradingpair))
            {
                ((RESTHelper)SubscribedTradingPairs[tradingpair]).TradeInfo.d = d;
            }
        }
        /// <summary>
        /// 更新深度数据
        /// </summary>
        /// <param name="t"></param>
        protected void UpdateTicker(string tradingpair, Ticker t)
        {
            if (SubscribedTradingPairs != null)
                if (SubscribedTradingPairs.ContainsKey(tradingpair))
            {
                ((RESTHelper)SubscribedTradingPairs[tradingpair]).TradeInfo.t=t;
            }
        }
        protected string Trade(OrderType type, string tradingpair,double price, double amount)
        {
            CheckSet();
            string url = GetPublicApiURL("", "trade.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            RestRequest rr = new RestRequest(Method.POST);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("symbol", tradingpair);
            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            paras.Add("symbol", tradingpair);
            switch (type)
            {
                case OrderType.ORDER_TYPE_BUY:
                    paras.Add("type", "buy");
                    paras.Add("price", price.ToString());
                    rr.AddParameter("type", "buy");
                    rr.AddParameter("price", price.ToString());
                    break;
                case OrderType.ORDER_TYPE_SELL:
                    paras.Add("type", "sell");
                    paras.Add("price", price.ToString());
                    rr.AddParameter("type", "sell");
                    rr.AddParameter("price", price.ToString());
                    break;
                case OrderType.ORDER_TYPE_MARKETBUY:
                    paras.Add("type", "buy_market");
                    rr.AddParameter("type", "buy_market");
                    break;
                case OrderType.ORDER_TYPE_MARKETSELL:
                    paras.Add("type", "sell_market");
                    rr.AddParameter("type", "sell_market");
                    break;

            }
            paras.Add("amount", amount.ToString());
            rr.AddParameter("amount", amount.ToString());
            String sign = MD5Util.buildMysignV1(paras, _secret);
            rr.AddParameter("sign", sign);
            
            var response = new RestClient(url).Execute(rr);


            string rawresponse = response.Content;

            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if (!Convert.ToBoolean(obj["result"]))
                {

                     throw (new Exception("error:" + SpotErrcode2Msg.Prase(obj["error_code"].ToString())));
                }
                return obj["order_id"].ToString();
            }
            catch (Exception e)
            {
                throw e;
            }
           
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradepair"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public string GetPublicApiURL(string tradepair, string method)
        {
            //https://www.okex.com/api/v1/ticker.do?symbol=ltc_btc
            return ApiUrl + method +  tradepair;
        }

       public string GetLocalTradingPairString(TradePair t, SubscribeTypes st = CommonLab.SubscribeTypes.RESTAPI)
        {
            if (st == SubscribeTypes.WSS)
            {
                if (t.FromSymbol.ToLower() == "btc" && t.ToSymbol.ToLower() == "usd")
                    return "";
                return t.FromSymbol.ToLower()+"_" + t.ToSymbol.ToLower();
            }
            else if (st == SubscribeTypes.RESTAPI)
            {
                return t.FromSymbol.ToLower() + "_"+ t.ToSymbol.ToLower();
            }
            return t.FromSymbol.ToLower() + t.ToSymbol.ToLower();
        }

        public Account GetAccount(out string rawresponse)
        {
            CheckSet();
            if (_account == null)
                _account = new Account();
            string url = GetPublicApiURL("", "userinfo.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            RestRequest rr = new RestRequest(Method.POST);
            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            String sign = MD5Util.buildMysignV1(paras,_secret);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("sign", sign);
            var response = new RestClient(url).Execute(rr);


            rawresponse = response.Content;
            //解析
            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if (!Convert.ToBoolean(obj["result"]))
                {
                    throw (new Exception("error:" + rawresponse));
                }              
                JToken borrow = obj["info"]["funds"]["borrow"];
                JToken free = obj["info"]["funds"]["free"];
                JToken freezed = obj["info"]["funds"]["freezed"];
                foreach (JProperty jp in borrow)
                {
                    if (_account.Balances.ContainsKey(jp.Name))
                    {
                        _account.Balances[jp.Name].borrow = Convert.ToDouble(jp.Value.ToString());
                        _account.Balances[jp.Name].available = Convert.ToDouble(free[jp.Name].ToString());
                        _account.Balances[jp.Name].reserved = Convert.ToDouble(freezed[jp.Name].ToString());
                        _account.Balances[jp.Name].balance = _account.Balances[jp.Name].available + _account.Balances[jp.Name].reserved;
                    }
                    else
                    {
                        Balance b = new Balance();
                        b.borrow = Convert.ToDouble(jp.Value.ToString());
                        b.available = Convert.ToDouble(free[jp.Name].ToString());
                        b.reserved = Convert.ToDouble(freezed[jp.Name].ToString());
                        b.balance = b.available + b.reserved;

                        _account.Balances.Add(jp.Name, b);
                    }
                }
            }
            catch(Exception e)
            {
                
                throw e;
            }
            return _account;
        }

        public Order GetOrderStatus(string OrderID,string tradingpair, out string rawresponse)
        {
            CheckSet();
            Order order = null;
            string url = GetPublicApiURL("", "order_info.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            RestRequest rr = new RestRequest(Method.POST);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("symbol", tradingpair);
            rr.AddParameter("order_id", OrderID);
          
            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            paras.Add("symbol", tradingpair);
            paras.Add("order_id", OrderID);
            String sign = MD5Util.buildMysignV1(paras, _secret);
            rr.AddParameter("sign", sign);
            var response = new RestClient(url).Execute(rr);


            rawresponse = response.Content;
            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if (!Convert.ToBoolean(obj["result"]))
                {
                    throw (new Exception("error:" + rawresponse));
                }
                JArray orders = JArray.Parse(obj["orders"].ToString());
                if (orders.Count > 0)
                {
                    order = new Order();
                    order.Id = orders[0]["order_id"].ToString();
                    order.Amount=Convert.ToDouble( orders[0]["amount"].ToString());
                    order.DealAmount= Convert.ToDouble(orders[0]["deal_amount"].ToString());
                    order.Price= Convert.ToDouble(orders[0]["price"].ToString());
                    order.Type = GetOrderTypeFromString(orders[0]["type"].ToString());
                    order.Status = GetOrderStatus(orders[0]["status"].ToString());
                    order.TradingPair = orders[0]["symbol"].ToString();
                    order.CreatDate = CommonLab.TimerHelper.ConvertStringToDateTime(Convert.ToDouble(orders[0]["create_date"].ToString()) / 1000);
                }
              
            }
            catch (Exception e)
            {
                Exception err = new Exception("订单获取解析json失败" + e.Message);

               // throw err;
            }
            return order;

        }
        public List<Order> GetHisOrders(string tradingpair)
        {
            CheckSet();
            string rawresponse;
            Order order = null;
            List<Order> orders_array = new List<Order>();
            string url = GetPublicApiURL("", "order_history.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            int page = 1;
            RestRequest rr = new RestRequest(Method.POST);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("symbol", tradingpair);
            rr.AddParameter("status", 1);
            rr.AddParameter("current_page", page);
            rr.AddParameter("page_length", 200);

            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            paras.Add("symbol", tradingpair);
            paras.Add("status", "1");
            paras.Add("current_page", page.ToString());
     
            paras.Add("page_length", "200");
            String sign = MD5Util.buildMysignV1(paras, _secret);
            rr.AddParameter("sign", sign);
            var response = new RestClient(url).Execute(rr);

            int total;
            int totalpage = 0;
            rawresponse = response.Content;
            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if (obj.Property("error_code") != null)
                    throw (new Exception(SpotErrcode2Msg.Prase(obj["error_code"].ToString())));
                if (!Convert.ToBoolean(obj["result"]))
                {
                    throw (new Exception("error:" + rawresponse));
                }
                JArray orders = JArray.Parse(obj["orders"].ToString());
                total=Convert.ToInt32(obj["total"].ToString());
                totalpage = total / 200+1;
                if (orders.Count > 0)
                {
                    
                    for (int i = 0; i < orders.Count; i++)
                    {


                        order = new Order();
                        order.Id = orders[i]["order_id"].ToString();
                        order.Amount = Convert.ToDouble(orders[i]["amount"].ToString());
                        order.DealAmount = Convert.ToDouble(orders[i]["deal_amount"].ToString());
                        order.Price = Convert.ToDouble(orders[i]["price"].ToString());
                        order.Type = GetOrderTypeFromString(orders[i]["type"].ToString());
                        order.Status = GetOrderStatus(orders[i]["status"].ToString());
                        order.TradingPair = orders[i]["symbol"].ToString();
                        order.CreatDate = CommonLab.TimerHelper.ConvertStringToDateTime(Convert.ToDouble(orders[i]["create_date"].ToString()) / 1000);
                        orders_array.Add(order);
                    }
                   
                }
                for (page = 2; page <= totalpage; page++)
                {
                    rr.Parameters.Clear();
                    paras.Clear();
                    rr.AddParameter("api_key", _key);
                    rr.AddParameter("symbol", tradingpair);
                    rr.AddParameter("status", 1);
                    rr.AddParameter("current_page", page);
                    rr.AddParameter("page_length", 200);

                   
                    paras.Add("api_key", _key);
                    paras.Add("symbol", tradingpair);
                    paras.Add("status", "1");
                    paras.Add("current_page", page.ToString());

                    paras.Add("page_length", "200");
                    sign = MD5Util.buildMysignV1(paras, _secret);
                    rr.AddParameter("sign", sign);
                    response = new RestClient(url).Execute(rr);

                   
                    rawresponse = response.Content;
                    obj = JObject.Parse(rawresponse);
                    if (obj.Property("error_code") != null)
                        throw (new Exception(SpotErrcode2Msg.Prase(obj["error_code"].ToString())));
                    if (!Convert.ToBoolean(obj["result"]))
                    {
                        throw (new Exception("error:" + rawresponse));
                    }
                    orders = JArray.Parse(obj["orders"].ToString());
                    if (orders.Count > 0)
                    {
                       
                        for (int i = 0; i < orders.Count; i++)
                        {


                            order = new Order();
                            order.Id = orders[i]["order_id"].ToString();
                            order.Amount = Convert.ToDouble(orders[i]["amount"].ToString());
                            order.DealAmount = Convert.ToDouble(orders[i]["deal_amount"].ToString());
                            order.Price = Convert.ToDouble(orders[i]["price"].ToString());
                            order.Type = GetOrderTypeFromString(orders[i]["type"].ToString());
                            order.Status = GetOrderStatus(orders[i]["status"].ToString());
                            order.TradingPair = orders[i]["symbol"].ToString();
                            order.CreatDate = CommonLab.TimerHelper.ConvertStringToDateTime(Convert.ToDouble(orders[i]["create_date"].ToString()) / 1000);
                            orders_array.Add(order);
                        }

                    }
                }
                return orders_array;

            }
            catch (Exception e)
            {
                Exception err = new Exception("订单获取解析json失败" + e.Message);
                throw err;
            }
      
        }
        public List<Order> GetOrdersStatus(string tradingpair, out string rawresponse)
        {
            CheckSet();
            Order order = null;
            string url = GetPublicApiURL("", "order_info.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            RestRequest rr = new RestRequest(Method.POST);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("symbol", tradingpair);
            rr.AddParameter("order_id", -1);

            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            paras.Add("symbol", tradingpair);
            paras.Add("order_id", "-1");
            String sign = MD5Util.buildMysignV1(paras, _secret);
            rr.AddParameter("sign", sign);
            var response = new RestClient(url).Execute(rr);


            rawresponse = response.Content;
            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if(obj.Property("error_code")!=null)
                    throw (new Exception(SpotErrcode2Msg.Prase(obj["error_code"].ToString())));
                if (!Convert.ToBoolean(obj["result"]))
                {
                    throw (new Exception("error:" + rawresponse));
                }
                JArray orders = JArray.Parse(obj["orders"].ToString());
                if (orders.Count > 0)
                {
                    List<Order> orders_array = new List<Order>();
                    for (int i = 0; i < orders.Count; i++)
                    {


                        order = new Order();
                        order.Id = orders[i]["order_id"].ToString();
                        order.Amount = Convert.ToDouble(orders[i]["amount"].ToString());
                        order.DealAmount = Convert.ToDouble(orders[i]["deal_amount"].ToString());
                        order.Price = Convert.ToDouble(orders[i]["price"].ToString());
                        order.Type = GetOrderTypeFromString(orders[i]["type"].ToString());
                        order.Status = GetOrderStatus(orders[i]["status"].ToString());
                        order.TradingPair = orders[i]["symbol"].ToString();
                        order.CreatDate = CommonLab.TimerHelper.ConvertStringToDateTime(Convert.ToDouble(orders[i]["create_date"].ToString()) / 1000);
                        orders_array.Add(order);
                    }
                    return orders_array;
                }

            }
            catch (Exception e)
            {
                Exception err = new Exception("订单获取解析json失败" + e.Message);
                throw err;
            }
            return null;

        }
        public bool CancelOrder(string OrderID,string tradingpair, out string rawresponse)
        {
            CheckSet();
            
            string url = GetPublicApiURL("", "cancel_order.do");
            RestClient rc = new RestClient(url);
            if (_proxy != null)
            {
                rc.Proxy = new WebProxy(_proxy.IP, Convert.ToInt32(_proxy.Port));
            }
            RestRequest rr = new RestRequest(Method.POST);
            rr.AddParameter("api_key", _key);
            rr.AddParameter("symbol", tradingpair);
            rr.AddParameter("order_id", OrderID);

            Dictionary<String, String> paras = new Dictionary<String, String>();
            paras.Add("api_key", _key);
            paras.Add("symbol", tradingpair);
            paras.Add("order_id", OrderID);
            String sign = MD5Util.buildMysignV1(paras, _secret);
            rr.AddParameter("sign", sign);
            var response = new RestClient(url).Execute(rr);


            rawresponse = response.Content;
            try
            {
                JObject obj = JObject.Parse(rawresponse);
                if (Convert.ToBoolean(obj["result"]))
                {
                    return true;
                }
               

            }
            catch (Exception e)
            {
                //这里应该抛出错误
            }
            return false ;
        }

        public bool CancelAllOrders(string tradingpair = "")
        {
            bool flag = false;
            try
            {
                if (SubscribedTradingPairs != null)
                {
                    foreach (KeyValuePair<string, SubscribeInterface> item in SubscribedTradingPairs)
                    {
                        string raw = "";
                        if(tradingpair.Length>0&&tradingpair!=item.Key)
                        {
                            continue;
                           
                        }
                        List<CommonLab.Order> orders = GetOrdersStatus(item.Key, out raw);
                        if (orders != null)
                        {
                            for (int i = 0; i < orders.Count; i++)
                            { CancelOrder(orders[i].Id, item.Key, out raw); }
                            flag = true;
                        }

                    }
                }
                return flag;
            }
            catch
            {
                return flag;
            }
            
        }

        public string Buy(string Symbol, double Price, double Amount)
        {
            if (Price > 0)
            {
                return Trade(OrderType.ORDER_TYPE_BUY, Symbol, Price, Amount);
            }
            else if(Price==0)
            {
                return Trade(OrderType.ORDER_TYPE_MARKETBUY, Symbol, Price, Amount);
            }
            return "";
        }

        public string Sell(string Symbol, double Price, double Amount)
        {
            if (Price > 0)
            {
                return Trade(OrderType.ORDER_TYPE_SELL, Symbol, Price, Amount);
            }
            else if (Price == 0)
            {
                return Trade(OrderType.ORDER_TYPE_MARKETSELL, Symbol, Price, Amount);
            }
            return "";
        }

        public void CheckSet()
        {
            if (string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_secret))
            {
                throw new Exception("交易所设置有问题！");
            }
        }
        static public OrderType GetOrderTypeFromString(string str)
        {
            if (str.ToLower() == "buy")
                return OrderType.ORDER_TYPE_BUY;
            if (str.ToLower() == "sell")
                return OrderType.ORDER_TYPE_SELL;
            if (str.ToLower() == "buy_market")
                return OrderType.ORDER_TYPE_MARKETBUY;
            if (str.ToLower() == "sell_market")
                return OrderType.ORDER_TYPE_MARKETSELL;
            return OrderType.ORDER_TYPE_UNKOWN;
        }
        static public OrderStatus GetOrderStatus(string str)
        {
            //status:-1:已撤销  0:未成交  1:部分成交  2:完全成交 4:撤单处理中
            if (str.ToLower() == "-1")
                return OrderStatus.ORDER_STATE_CANCELED;
            if (str.ToLower() == "0")
                return OrderStatus.ORDER_STATE_PENDING;
            if (str.ToLower() == "1")
                return OrderStatus.ORDER_STATE_PARTITAL;
            if (str.ToLower() == "2")
                return OrderStatus.ORDER_STATE_SUCCESS;
            if (str.ToLower() == "4")
                return OrderStatus.ORDER_STATE_CANCELING;
            return OrderStatus.ORDER_STATE_UNKOWN;
        }
        private Exception JsonError(JObject obj)
        {
            if (obj.Property("error_code") == null || obj.Property("error_code").ToString() == "")
            {
                return new Exception("\"error_code\" not found in raw json string!");
            }
            else
                return new Exception(SpotErrcode2Msg.Prase(obj["error_code"].ToString()));
        }

        public void DisSubcribe(TradePair tp, SubscribeTypes st)
        {
            throw new NotImplementedException();
        }


    }
}
