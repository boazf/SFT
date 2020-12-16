using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UDPClient
{
    class Program
    {
        const int SntpPort = 123;
        static DateTime BaseDate = new DateTime(1900, 1, 1);

        static void Main(string[] args)
        {
            //UdpClient udp = new UdpClient("time.nist.gov", 123);
            //byte[] packetBuffer = new byte[16];
            //packetBuffer[0] = 0b11100011;   // LI, Version, Mode
            //packetBuffer[1] = 0;     // Stratum, or type of clock
            //packetBuffer[2] = 6;     // Polling Interval
            //packetBuffer[3] = 0xEC;  // Peer Clock Precision
            //                         // 8 bytes of zero for Root Delay & Root Dispersion
            //packetBuffer[12] = 49;
            //packetBuffer[13] = 0x4E;
            //packetBuffer[14] = 49;
            //packetBuffer[15] = 52;
            //udp.Send(packetBuffer, packetBuffer.Length);
            //IPEndPoint remoteEndpoint = null;
            //byte[] response = udp.Receive(ref remoteEndpoint);
            //uint numberOfSeconds;

            //if (BitConverter.IsLittleEndian)
            //    numberOfSeconds = BitConverter.ToUInt32(
            //        response.Skip(40).Take(4).Reverse().ToArray()
            //        , 0);
            //else
            //    numberOfSeconds = BitConverter.ToUInt32(response, 40);

            //var date = BaseDate.AddSeconds(numberOfSeconds);

            //Console.WriteLine(
            //    $"Current date in server: {date:yyyy-MM-dd HH:mm:ss}");
            if (args.Length == 0)
            {
                Console.WriteLine("Simple SNTP client");
                Console.WriteLine();
                Console.WriteLine("Usage: sntpclient <sntp server url> [<local timezone>]");
                Console.WriteLine();
                Console.WriteLine("<local timezone>: a number between -12 and 12 as hours from UTC");
                Console.WriteLine("(append .5 for an extra half an hour)");
                return;
            }

            double localTimeZoneInHours = 0;
            if (args.Length > 1)
                localTimeZoneInHours = double.Parse(args[1], CultureInfo.InvariantCulture);

            var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;

            var sntpRequest = new byte[48];
            sntpRequest[0] = 0x23; //LI=0 (no warning), VN=4, Mode=3 (client)

            udpClient.Send(
                dgram: sntpRequest,
                bytes: sntpRequest.Length,
                hostname: args[0],
                port: SntpPort);

            byte[] sntpResponse;
            try
            {
                IPEndPoint remoteEndpoint = null;
                sntpResponse = udpClient.Receive(ref remoteEndpoint);
            }
            catch (SocketException)
            {
                Console.WriteLine("*** No response received from the server");
                return;
            }

            uint numberOfSeconds;
            if (BitConverter.IsLittleEndian)
                numberOfSeconds = BitConverter.ToUInt32(
                    sntpResponse.Skip(40).Take(4).Reverse().ToArray()
                    , 0);
            else
                numberOfSeconds = BitConverter.ToUInt32(sntpResponse, 40);

            var date = BaseDate.AddSeconds(numberOfSeconds).AddHours(localTimeZoneInHours);

            Console.WriteLine(
                $"Current date in server: {date:yyyy-MM-dd HH:mm:ss} UTC{localTimeZoneInHours:+0.#;-0.#;.}");
        }
    }
}

