using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace StockServer
{
    class Program
    {
        static void Main(string[] args)
        {
            StockMarketServer server = new StockMarketServer();
            server.RunServer();
        }
    }
}
