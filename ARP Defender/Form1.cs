using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;

namespace ARP_Defender
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent(); //初始化組件
            hostip_label.Text = GetHostIPAddress();
            hostmac_label.Text = GetHostMACAddress();
            gatewayip_label.Text = GetGatewayIPAddress().ToString();
            gatewaymac_label.Text = GetGatewayMACAddress(GetGatewayIPAddress().ToString());

            //test_label.Text = GetNetworkAdapter();
            //test_label.Text = "set neighbors" + " " + GetNetworkAdapter() + " " + GetGatewayIPAddress().ToString() + " " + GetGatewayMACAddress(GetGatewayIPAddress().ToString());
        }

        private void start_Click(object sender, EventArgs e)
        {
            show_label.Text = "開啟防禦";
            show_label.ForeColor = Color.Green;
            String cmdstr = "set neighbors" + " " + GetNetworkAdapter() + " " + GetGatewayIPAddress().ToString() + " " + GetGatewayMACAddress(GetGatewayIPAddress().ToString());
            CMDARPstatic(cmdstr);
        }

        private void stop_Click(object sender, EventArgs e)
        {
            show_label.Text = "尚未開啟防禦";
            show_label.ForeColor = Color.Red;
            String cmdstr = "delete neighbors" + " " + GetNetworkAdapter() + " " + GetGatewayIPAddress().ToString();
            CMDARPdeletestatic(cmdstr);
        }

        public string GetHostIPAddress()
        {
            string localIP = string.Empty;
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();

            return localIP;
        }

        public string GetHostMACAddress()
        {
            Dictionary<string, long> macAddresses = new Dictionary<string, long>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses[nic.GetPhysicalAddress().ToString()] = nic.GetIPStatistics().BytesSent + nic.GetIPStatistics().BytesReceived;
                }    
            }
            long maxValue = 0;
            string mac = "";
            foreach (KeyValuePair<string, long> pair in macAddresses)
            {
                if (pair.Value > maxValue)
                {
                    mac = pair.Key;
                    maxValue = pair.Value;
                }
            }
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1-$2-$3-$4-$5-$6";
            var mac_newformat = Regex.Replace(mac, regex, replace);
            return mac_newformat.ToString();
        }

        public static IPAddress GetGatewayIPAddress(IPAddress netaddr = null)
        {
            // user can provide an ip address that exists on the network they want to connect to, 
            // or this routine will default to 8.8.8.8 (IP of a popular internet dns provider)
            if (netaddr is null)
            {
                netaddr = IPAddress.Parse("8.8.8.8");
            }
                
            PingReply reply = default;
            var ping = new Ping();
            var options = new PingOptions(1, true); // ttl=1, dont fragment=true
            try
            {
                // I arbitrarily used a 200ms timeout; tune as you see fit.
                reply = ping.Send(netaddr, 200, new byte[0], options);
            }
            catch (PingException)
            {
                System.Diagnostics.Debug.WriteLine("找不到預設閘道 IP 位址");
                return default;
            }
            if (reply.Status != IPStatus.TtlExpired)
            {
                System.Diagnostics.Debug.WriteLine("找不到預設閘道 IP 位址");
                return default;
            }
            return reply.Address;
        }

        public string GetGatewayMACAddress(string GatewayIP)
        {
            string dirResults = "";
            ProcessStartInfo psi = new ProcessStartInfo();
            Process proc = new Process();
            psi.FileName = "arp";
            psi.RedirectStandardInput = false;
            psi.RedirectStandardOutput = true;
            psi.Arguments = "-a " + GatewayIP;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            try
            {
                proc = Process.Start(psi);
                dirResults = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
            }
            catch (Exception)
            { }

            Match m = Regex.Match(dirResults, "\\w+\\-\\w+\\-\\w+\\-\\w+\\-\\w+\\-\\w\\w");

            if (m.ToString() != "")
            {
                return m.ToString();
            }
            else
            {
                return "找不到預設閘道 MAC 位址";
            }
        }

        public static void CMDARPstatic(String cmdstr)
        {
            Process CmdProcess = new Process(); //建立執行CMD
            CmdProcess.StartInfo.FileName = "cmd.exe";
            CmdProcess.StartInfo.CreateNoWindow = true;         //不建立新視窗    
            CmdProcess.StartInfo.UseShellExecute = false;       //不使用shell啟動  
            CmdProcess.StartInfo.RedirectStandardInput = true;  
            CmdProcess.StartInfo.RedirectStandardOutput = true;
            CmdProcess.StartInfo.RedirectStandardError = true;
            CmdProcess.Start(); //執行 

            CmdProcess.StandardInput.WriteLine("netsh -c \"interface ipv4\"");
            CmdProcess.StandardInput.WriteLine(cmdstr);
            CmdProcess.StandardInput.WriteLine("exit");
 
            //CmdProcess.WaitForExit();//等待程式執行完退出程序   
            CmdProcess.Close(); //結束 
        }

        public static void CMDARPdeletestatic(String cmdstr)
        {
            Process CmdProcess = new Process(); //建立執行CMD
            CmdProcess.StartInfo.FileName = "cmd.exe";
            CmdProcess.StartInfo.CreateNoWindow = true;         //不建立新視窗    
            CmdProcess.StartInfo.UseShellExecute = false;       //不使用shell啟動  
            CmdProcess.StartInfo.RedirectStandardInput = true;
            CmdProcess.StartInfo.RedirectStandardOutput = true;
            CmdProcess.StartInfo.RedirectStandardError = true;
            CmdProcess.Start(); //執行 

            CmdProcess.StandardInput.WriteLine("netsh -c \"interface ipv4\"");
            CmdProcess.StandardInput.WriteLine(cmdstr);
            CmdProcess.StandardInput.WriteLine("exit");
            CmdProcess.Close(); //結束
        }

        public string GetNetworkAdapter()
        {
            string NetworkAdapter = string.Empty;
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    NetworkAdapter = nic.NetworkInterfaceType.ToString();
                }
            }

            if(NetworkAdapter == "Wireless80211")
            {
                NetworkAdapter = "Wi-Fi";
            }
            else if(NetworkAdapter == "Ethernet")
            {
                NetworkAdapter = "乙太網路"; //需要知道編號...
            }

            return NetworkAdapter;
        }
    }
}