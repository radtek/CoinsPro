﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonLab;
using WebSocketSharp;
using Newtonsoft.Json.Linq;

namespace KFCC.EHuobiExchange
{
    class WssHelper : KFCC.ExchangeInterface.SubscribeInterface
    {
        //wss://real.okex.com:10441/websocket
        const string spotwssurl = "wss://api.huobi.pro/ws";
        const string futhurewssurl = "";
        private string _tradingpair;
        private string _appkey;
        private string Cache = "";

    
        private TradingInfo _tradinginfo;
        public event ExchangeEventWarper.TradeInfoEventHander TradeInfoEvent;
        

        private WebSocket ws ;
        public TradingInfo TradeInfo
        {
            get { return _tradinginfo; }
        }
        public WssHelper(CommonLab.TradePair tp, Ticker t, Depth d)
        {
            _tradingpair = tp.FromSymbol.ToLower() + tp.ToSymbol.ToLower();
            _tradinginfo = new TradingInfo(SubscribeTypes.WSS, _tradingpair);
            _tradinginfo.t = t;
            _tradinginfo.d = d;

            ws = new WebSocket(spotwssurl);
            ws.OnOpen += (sender, e) =>
            {
                ws.Send("{\"sub\": \"market." + _tradingpair + ".kline.1min\",  \"id\": \"id" + DateTime.Now.Second + "\"}");
                ws.Send("{\"sub\": \"market." + _tradingpair + ".depth.step0\",  \"id\": \"id" + DateTime.Now.Second + "\"}");
            };

            ws.OnMessage += (sender, e) =>
            {

                if (e.IsBinary)
                {
                    JObject obj = null;
                    string str = CommonLab.GZipUtil.UnZip(e.RawData);
                    try
                    {
                        obj = JObject.Parse(str);
                    }
                    catch
                    {
                        Console.WriteLine(str);


                        return;
                    }




                    if (obj.Property("ping") != null)
                    {
                        long pong = Convert.ToInt64(obj["ping"].ToString());
                        ws.Send("{\"pong\": " + pong + "}");
                    }
                    if (obj.Property("status") != null)
                    {
                        if (obj["status"].ToString() == "error")
                        {
                            Exception err = new Exception(obj["err-msg"].ToString());
                            throw err;
                        }
                    }
                    if (obj.Property("ch") != null)
                    {
                        string ch = obj["ch"].ToString();
                        if (ch.IndexOf("kline") > 0) //kline数据
                        {

                            try
                            {
                                JObject ticker = JObject.Parse(obj["tick"].ToString());
                                t.High = Convert.ToDouble(ticker["high"].ToString());
                                t.Low = Convert.ToDouble(ticker["low"].ToString());
                                t.Last = Convert.ToDouble(ticker["close"].ToString());
                                //t.Sell = Convert.ToDouble(JArray.Parse(ticker["ask"].ToString())[0]);
                                //t.Buy = Convert.ToDouble(JArray.Parse(ticker["bid"].ToString())[0]);
                                t.Volume = Convert.ToDouble(ticker["vol"].ToString());
                                t.Open = Convert.ToDouble(ticker["open"].ToString()); ;// Convert.ToDouble(ticker["open"].ToString());
                                t.ExchangeTimeStamp = Convert.ToDouble(obj["ts"].ToString()) / 1000;
                                t.LocalServerTimeStamp = CommonLab.TimerHelper.GetTimeStamp(DateTime.Now);
                                //UpdateTicker(tradingpair, t);
                                _tradinginfo.t = t;

                                TradeInfoEvent(_tradinginfo, TradeEventType.TRADE);
                            }
                            catch (Exception err)
                            {
                                throw err;
                            }
                        }
                        if (ch.IndexOf("depth") > 0) //深度数据
                        {
                            JArray jasks = JArray.Parse(obj["tick"]["asks"].ToString());
                            for (int i = 0; i < jasks.Count; i++)
                            {
                                MarketOrder m = new MarketOrder();
                                m.Price = Convert.ToDouble(JArray.Parse(jasks[i].ToString())[0]);
                                m.Amount = Convert.ToDouble(JArray.Parse(jasks[i].ToString())[1]);
                                _tradinginfo.d.AddNewAsk(m);
                            }

                            JArray jbids = JArray.Parse(obj["tick"]["bids"].ToString());
                            for (int i = 0; i < jbids.Count; i++)
                            {
                                MarketOrder m = new MarketOrder();
                                m.Price = Convert.ToDouble(JArray.Parse(jbids[i].ToString())[0]);
                                m.Amount = Convert.ToDouble(JArray.Parse(jbids[i].ToString())[1]);
                                _tradinginfo.d.AddNewBid(m);
                            }
                            _tradinginfo.d.ExchangeTimeStamp = Convert.ToDouble(obj["tick"]["ts"]) / 1000;// Convert.ToDouble(obj["timestamp"].ToString());
                            _tradinginfo.d.LocalServerTimeStamp = CommonLab.TimerHelper.GetTimeStamp(DateTime.Now);

                            if ((_tradinginfo.t.Buy != _tradinginfo.d.Bids[0].Price) || (_tradinginfo.t.Sell != _tradinginfo.d.Asks[0].Price))
                            {
                                TradeInfoEvent(_tradinginfo, TradeEventType.TICKER);
                            }
                            TradeInfoEvent(_tradinginfo, TradeEventType.ORDERS);
                        }
                        if (ch.IndexOf("trade") > 0) //交易数据
                        {
                        }
                        if (ch.IndexOf("detail") > 0) //市场数据
                        {
                        }
                    }



                }
            };
            ws.Connect();
        }

        public void Close()
        {
            if (ws != null)
            {
                ws.Close();
            }
        }
    }
}