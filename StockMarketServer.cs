using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace StockServer
{
    class StockMarketServer
    {
        private const int port = 10001;
        private long currentHolder;
        private long nextTraderId;
        private List<long> traderIDs;
        private Dictionary<long, StreamWriter> allWriters;
        private Dictionary<long, StreamReader> allReaders;
        private object _lock = new object();

        public StockMarketServer()
        {
            currentHolder = 0;
            nextTraderId = 0;
            allWriters = new Dictionary<long, StreamWriter>();
            allReaders = new Dictionary<long, StreamReader>();
            traderIDs = new List<long>();
        }

        public void RunServer()
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                Console.WriteLine("Waiting for incoming connections...");

                while (true)
                {
                    TcpClient tcpClient = listener.AcceptTcpClient();
                    Stream stream = tcpClient.GetStream();
                    StreamWriter writer = new StreamWriter(stream);
                    StreamReader reader = new StreamReader(stream);

                    long traderId = GetNextTraderId();

                    if (currentHolder == 0)
                    {
                        currentHolder = traderId;
                        Console.WriteLine($"Stock has been given to Trader {currentHolder}");
                    }

                    allReaders.Add(traderId, reader);
                    allWriters.Add(traderId, writer);
                    traderIDs.Add(traderId);

                    ThreadStart startReader = () => ProcessIncomingRequests(reader, traderId);
                    new Thread(startReader).Start();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }



        private void ProcessIncomingRequests(StreamReader reader, long traderId)
        {
            using (reader)
            {
                try
                {
                    string command = reader.ReadLine();

                    if (command == "CONNECT")
                    {
                        ProcessNewConnection(traderId);
                        BroadcastNewTraderJoined(traderId);
                    }
                    else
                    {
                        Console.WriteLine($"Unexpected Command: {command}.");
                    }

                    while (true)
                    {
                        command = reader.ReadLine();
                        string[] substrings = command.Split(' ');

                        switch (substrings[0].ToUpper())
                        {
                            case "START_TRADE":

                                if (long.TryParse(substrings[1], out long tradeTo))
                                {
                                    if (ProcessTradeRequest(tradeTo))
                                    {
                                        Console.Write($"\nStock has been transferred from Trader {traderId} to {tradeTo}.\n");
                                        BroadcastNewStockHolder(traderId, tradeTo);
                                    }
                                }
                                break;

                            default:
                                throw new Exception($"Unknown command: {command}.");
                        }
                    }
                }
                catch
                {
                    DisconnectTrader(traderId);
                }
            }
        }

        private void AllocateNewStockHolder(long traderId)
        {
            if (traderIDs.Count > 0)
            {
                long newStockHolder = traderIDs.FirstOrDefault();
                StreamWriter writerNew = allWriters[newStockHolder];
                writerNew.WriteLine("RECEIVE_TRADE");
                writerNew.Flush();
                BroadcastNewStockHolder(traderId, newStockHolder);
                currentHolder = newStockHolder;
                Console.WriteLine($"Stock has been given to Trader {newStockHolder}");
                PrintTradersInMarket();
            }
            else
            {
                currentHolder = 0;
            }
        }

        private void DisconnectTrader(long traderId)
        {     
            Console.WriteLine($"\nTrader {traderId} disconnected.");

            allWriters.Remove(traderId);
            traderIDs.Remove(traderId);

            BroadcastTraderDisconnected(traderId);

            if (currentHolder == traderId)
            {
                AllocateNewStockHolder(traderId);
            }           
        }

        private bool ProcessTradeRequest(long newHolder)
        {

            try
            {
                StreamWriter writerNew = allWriters[newHolder];
                StreamWriter writerCurrent = allWriters[currentHolder];

                if (writerNew != null && writerCurrent != null)
                {
                    writerNew.WriteLine("RECEIVE_TRADE");
                    writerNew.Flush();
                    writerCurrent.WriteLine("CONFIRM_TRADE");
                    writerCurrent.WriteLine(newHolder);
                    writerCurrent.Flush();
                    currentHolder = newHolder;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void BroadcastNewTraderJoined(long traderId)
        {
            foreach (long id in allWriters.Keys)
            {
                if (id != traderId)
                {
                    StreamWriter writer = allWriters[id];
                    writer.WriteLine("NEW_TRADER");
                    writer.WriteLine(traderId);
                    writer.Flush();
                }               
            }
        }

        private void BroadcastTraderDisconnected(long traderId)
        {
            foreach (long id in allWriters.Keys)
            {
                if (id != traderId)
                {
                    StreamWriter writer = allWriters[id];
                    writer.WriteLine("TRADER_DISCONNECTED");
                    writer.WriteLine(traderId);
                    writer.Flush();
                }
            }
            PrintTradersInMarket();
        }

        private void ProcessNewConnection(long traderId)
        {
            StreamWriter writer = allWriters[traderId];

            writer.WriteLine(traderId);
            writer.WriteLine(currentHolder);
            writer.WriteLine(traderIDs.Count);

            foreach (long trader in traderIDs)
            {
                writer.WriteLine(trader);
            }

            writer.Flush();

            Console.WriteLine($"\nNew connection; Trader ID {traderId}"); 
            PrintTradersInMarket();
        }

        private void BroadcastNewStockHolder(long traderId, long tradeTo)
        {
            foreach (long id in allWriters.Keys)
            {
                if (id != traderId && id != tradeTo)
                {
                    StreamWriter writer = allWriters[id];
                    writer.WriteLine("NEW_STOCKHOLDER");
                    writer.WriteLine(tradeTo);
                    writer.Flush();
                }
            }
        }

        private void PrintTradersInMarket()
        {
            if (traderIDs.Count > 0)
            {
                Console.WriteLine("\nTraders currently in the market:");
                foreach (long l in traderIDs)
                {
                    Console.WriteLine($"  Trader {l}");
                }
            }
            else
            {
                Console.WriteLine("\nNo traders currently in market.");
            }
        }

        private long GetNextTraderId()
        {
            long traderId;

            lock (_lock)
            {
                nextTraderId++;
                traderId = nextTraderId;
            }
            return traderId;
        }
    }
}
