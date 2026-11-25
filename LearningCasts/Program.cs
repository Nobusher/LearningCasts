using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LearningCasts
{
    internal class Program
    {
        private static readonly IPAddress GroupAddress = IPAddress.Parse("239.1.1.1");
        private const int Port = 5000;
        static async Task Main(string[] args)
        {
            
            Console.WriteLine("1 - Reciever");
            Console.WriteLine("2 - Sender");
            Console.WriteLine("3 - Request permission");
            Console.Write("Select option: ");
            string? choice = Console.ReadLine();

            try
            {
                switch (choice) 
                {
                    case "1":
                        await RunRecieverAsync();
                        break;
                    case "2":
                        await RunSenderAsync();
                        break;
                    case "3":
                        await AllowUdpInFirewallAsync();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Network error! {ex.Message}");
            }
        }
        private static async Task AllowUdpInFirewallAsync() 
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                Arguments = 
                "advfirewall firewall add rule " +
                $"name=\"Multicast Demo UDP {Port}\" " +
                $"dir=in action=allow protocol=UDP localport={Port} " +
                $"remoteip=localsubnet profile=any",
                UseShellExecute = true,
                Verb = "runas"
            };
            try
            {
                using Process? process = Process.Start(startInfo);

                if (process == null)
                {
                    Console.WriteLine("Permission denied...");
                    return;
                }
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Rule created for {Port}");
                }
                else
                {
                    Console.WriteLine($"Failed to add a rule. Exit code {process.ExitCode}");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Console.WriteLine("Request failed. Rule was not added");
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine($"Failed to start netsh {ex.Message}");
            }
        }
        private static async Task RunRecieverAsync() 
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
           

            udp.ExclusiveAddressUse = false;

            udp.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
                );

            udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            IPAddress recieverIp = IPAddress.Parse("192.168.88.56");
            udp.JoinMulticastGroup(GroupAddress, recieverIp);

            Console.WriteLine($"Listeting to group {GroupAddress} on port {Port}");
            Console.WriteLine("Ctrl+C to cancel");

            while (true) 
            {
                UdpReceiveResult packet = await udp.ReceiveAsync();
                string message = Encoding.UTF8.GetString(packet.Buffer);

                Console.WriteLine($"From {packet.RemoteEndPoint}: {message}");
            }
        }
        private static async Task RunSenderAsync() 
        {
            IPAddress senderIp = IPAddress.Parse("192.168.88.56");
            using var udp = new UdpClient(AddressFamily.InterNetwork);

            udp.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                senderIp.GetAddressBytes());

            var destination = new IPEndPoint(GroupAddress, Port);

            udp.MulticastLoopback = true;
            Console.WriteLine($"Sending to group {GroupAddress} on port {Port}");
            Console.WriteLine("Enter the message below");

            while (true) 
            {
                Console.Write("> ");
                string? message = Console.ReadLine();

                if (message == null || message == "/exit") 
                {
                    return;
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                await udp.SendAsync(data, data.Length, destination);
            }
        }
    }
}
