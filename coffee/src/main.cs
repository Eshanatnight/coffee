using System.Net;
using System.Net.Sockets;
using System.Text;
using Latte;
using Newtonsoft.Json;          // nuget Package

namespace Coffee
{
    public class Brew
    {
        private List<Packet> packets;
        private List<int> missingSequences;

        Brew()
        {
            packets = new();
            missingSequences = new();
        }

        public static void Main(string[] args)
        {
            Brew app = new();
            RequestAllPackets(app.packets, app.missingSequences);

            RequestResendPackets(app.packets, app.missingSequences);

            // sort the packets by sequence number
            app.packets.Sort((x, y) => x.packetSequence.CompareTo(y.packetSequence));

            string json = SerializeToJson(app.packets);
            WriteToJsonFile("./output.json", json);
        }


        // wrapper to Request the missing packets from the server
        public static void RequestResendPackets(List<Packet> packets, List<int> missingSequences)
        {
            using TcpClient client = new("127.0.0.1", 3000);
            using NetworkStream stream = client.GetStream();
            using BinaryWriter writer = new(stream);
            using BinaryReader reader = new(stream);
            if (missingSequences.Count > 0)
            {
                foreach (int missingSequence in missingSequences)
                {
                    ReceiveMissingPacket(reader, writer, packets, missingSequence);
                }
            }
            else
            {
                Console.WriteLine("No Missing Sequences");
            }

            client.Close();
        }


        // wrapper to Request all the packets from the server
        public static void RequestAllPackets(List<Packet> packets, List<int> missingSequences)
        {
            using TcpClient client = new("127.0.0.1", 3000);
            using NetworkStream stream = client.GetStream();
            using BinaryWriter writer = new(stream);
            using BinaryReader reader = new(stream);
            SentStreamAllPackets(writer);

            int expectedSequence = 1;
            while (true)
            {
                Packet? packet = ReceivePacket(reader);

                if (packet == null)
                {
                    break;
                }
                packets.Add(packet);
                int receivedSequence = packet.packetSequence;

                // in theory we should have all the packets in order
                // if i filter out the missing packets i can send the missing packets request?
                if (receivedSequence == expectedSequence)
                {
                    // Increment the expected sequence number
                    expectedSequence++;
                }
                else if (receivedSequence > expectedSequence)
                {
                    missingSequences.Add(expectedSequence);
                    expectedSequence = receivedSequence + 1;
                }
            }
        }


        // Send the request to stream all the packets
        public static void SentStreamAllPackets(BinaryWriter writer)
        {
            byte[] payload = new byte[2];
            payload[0] = 1; // callType

            // Send the request payload to the server
            // maybe add a try catch here
            try
            {
                writer.Write(payload, 0, payload.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception Thrown in SendStreamAllPackets: " + e.Message);
            }
        }


        // Receive the packet from the server
        public static Packet? ReceivePacket(BinaryReader reader)
        {
            try
            {
                string symbol = Encoding.ASCII.GetString(reader.ReadBytes(4));
                char buySellIndicator = reader.ReadChar();
                int quantity = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                int price = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                int packetSequence = IPAddress.NetworkToHostOrder(reader.ReadInt32());

                return new Packet
                {
                    symbol = symbol,
                    buySellIndicator = buySellIndicator,
                    quantity = quantity,
                    price = price,
                    packetSequence = packetSequence
                };
            }
            catch (EndOfStreamException)
            {
                return null; // End of data
            }
        }


        // Serialize the packets to json string
        public static string SerializeToJson(List<Packet> packets)
        {
            return JsonConvert.SerializeObject(packets, Formatting.Indented);
        }


        // Write the json string to file
        public static void WriteToJsonFile(string path, string content)
        {
            string cwd = Directory.GetCurrentDirectory();
            try
            {
                File.WriteAllText(cwd + "/output.json", content);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception Occured While Writing to file: " + e.Message);
            }
            Console.WriteLine("Written to file: output.json");
        }


        // Send the request to resend the missing packet
        static void SendResendPacketRequest(BinaryWriter writer, int sequenceToResend)
        {
            byte[] payload = new byte[2];
            payload[0] = 2; // callType
            payload[1] = (byte)sequenceToResend; // resendSeq (applicable only for callType 2)

            try
            {
                writer.Write(payload, 0, payload.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception Thrown in SendResendPacketRequest: " + e.Message);
            }
        }


        // Receive the missing packet and push it to the packets list
        static void ReceiveMissingPacket(BinaryReader reader, BinaryWriter writer, List<Packet> packets, int missingSequence)
        {
            SendResendPacketRequest(writer, missingSequence);
            // Receive and process the requested packet
            Packet? requestedPacket = ReceivePacket(reader);

            if (requestedPacket != null)
            {
                packets.Add(requestedPacket);
            }
        }
    }
}