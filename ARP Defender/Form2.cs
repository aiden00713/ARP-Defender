using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using System.Text.RegularExpressions;

namespace ARP_Defender
{
    public partial class Form2 : Form
    {
        String attack_ip = string.Empty;
        String attack_mac = string.Empty;
        //String testlable = string.Empty;
        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            var devices = LibPcapLiveDeviceList.Instance;
            int i = 0;
            foreach (var dev in devices)
            {
                if (dev.Interface.FriendlyName == GetNetworkAdapterName())
                {
                    break;
                }
                else
                {
                    i++;
                }
            }

            var device = devices[i];
            device.Open();
            //device.Filter = "arp"; //過濾ARP封包
            /* 當條件的封包被被截取時，執行 device_OnPacketArrival */
            device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);
            device.StartCapture();
        }

        public string GetNetworkAdapterName()
        {
            string NetworkAdapterName = string.Empty;
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    NetworkAdapterName = nic.Name.ToString();
                }
            }
            return NetworkAdapterName;
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
            return mac.ToString();
        }

        private void device_OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data); //封裝
                var arpPacket = packet.Extract<ArpPacket>(); //ARP封包

                if (arpPacket != null)
                {
                    //跨執行緒介面顯示處理
                    MethodInvoker mi = new MethodInvoker(this.UpdateUI);
                    this.BeginInvoke(mi, null);

                    //attack_ip = arpPacket.SenderProtocolAddress.ToString();
                    //attack_mac = arpPacket.SenderHardwareAddress.ToString();
                    //testlable = arpPacket.Operation.ToString();

                    if (arpPacket.Operation.ToString() == "Request") //只收集請求封包
                    {
                        if (arpPacket.SenderProtocolAddress.ToString() == GetHostIPAddress())
                        {
                            if (arpPacket.SenderHardwareAddress.ToString() != GetHostMACAddress())
                            {
                                attack_mac = arpPacket.SenderHardwareAddress.ToString();
                                attack_ip = GetARPIPaddress(attack_mac);
                                show_label.Text = "有人正在竄改你的 ARP 對應！";
                            }
                        }
                    }
                }
            }
            catch(Exception exce)
            {
                MessageBox.Show(exce.ToString(), "Error");
            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            var devices = LibPcapLiveDeviceList.Instance;
            int i = 0;
            foreach (var dev in devices)
            {
                if (dev.Interface.FriendlyName == GetNetworkAdapterName())
                {
                    break;
                }
                else
                {
                    i++;
                }
            }

            var device = devices[i];
            device.Close();
            device.StopCapture();

            this.Dispose();
            this.Close();
        }
        private void UpdateUI()
        {
            //test_label2.Text = testlable;
            attackip.Text = attack_ip;
            attackmac.Text = attack_mac;
        }

        public string GetARPIPaddress(string MACAddress)
        {
            string dirResults = string.Empty;
            ProcessStartInfo psi = new ProcessStartInfo();
            Process proc = new Process();
            psi.FileName = "arp";
            psi.RedirectStandardInput = false;
            psi.RedirectStandardOutput = true;
            psi.Arguments = "-a ";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            try
            {
                proc = Process.Start(psi);
                var line = proc.StandardOutput.ReadLine();
                proc.WaitForExit();

                line = line.Replace(" ", " ");
                var parts = line.Split(' ');

                if (parts.Length > 1 && parts[1] == MACAddress)
                {
                    dirResults =  parts[0];
                }
            }
            catch (Exception){ }

            return dirResults;
         }

        Timer myTimer = new Timer();
        int Count = 0;
        private void start_attackbutton_Click(object sender, EventArgs e)
        {
            stop_attackbutton.Enabled = true;
            start_attackbutton.Enabled = false;

            myTimer.Tick += new EventHandler(SendPacket);
            myTimer.Enabled = true;
            myTimer.Interval = 1000; //豪秒為單位，1秒執行1次
            show_label.Text = "開啟反擊！";
        }

        private void stop_attackbutton_Click(object sender, EventArgs e)
        {
            stop_attackbutton.Enabled = false;
            start_attackbutton.Enabled = true;
            myTimer.Stop();
            show_label.Text = "取消反擊！";
        }

        public void SendPacket(object sender, EventArgs e)
        {
            var devices = LibPcapLiveDeviceList.Instance;
            int i = 0;
            foreach (var dev in devices)
            {
                if (dev.Interface.FriendlyName == GetNetworkAdapterName())
                {
                    break;
                }
                else
                {
                    i++;
                }
            }

            //test_label.Text = devices[i].Interface.FriendlyName;
            var device = devices[i];
            device.Open();
            EthernetPacket eth = Send_ARPResponse_Packet();
            device.SendPacket(eth);
            Count++;
        }

        public EthernetPacket Send_ARPResponse_Packet()
        {
            string strEthDestMAC = GetGatewayMACAddress(GetGatewayIPAddress().ToString());
            string strEhSourMac = GetHostMACAddress();

            string strARPSourIP = attack_ip;
            string strARPSourMac = GetRandomWifiMacAddress();

            string strARPDestIP = GetGatewayIPAddress().ToString();
            string strARPDestMac = GetGatewayMACAddress(GetGatewayIPAddress().ToString());

            ArpPacket arp = new ArpPacket(ArpOperation.Response, PhysicalAddress.Parse(strARPDestMac), IPAddress.Parse(strARPDestIP), PhysicalAddress.Parse(strARPSourMac), IPAddress.Parse(strARPSourIP));
            EthernetPacket eth = new EthernetPacket(PhysicalAddress.Parse(strEhSourMac), PhysicalAddress.Parse(strEthDestMAC), EthernetType.Arp);
            eth.PayloadPacket = arp;
            return eth;
        }

        public static IPAddress GetGatewayIPAddress()
        {
            //透過ping Google DNS 達到traceroute效果，獲得主機預設閘道
            IPAddress netaddr = IPAddress.Parse("8.8.8.8");

            PingReply reply = default;
            var ping = new Ping();
            var options = new PingOptions(1, true); // ttl=1, dont fragment=true
            try
            {
                //200毫秒就 timeout
                reply = ping.Send(netaddr, 200, new byte[0], options);
            }
            catch (PingException)
            {
                MessageBox.Show("找不到預設閘道 IP 位址，可能設備沒有連上網際網路，請確認後再開啟本程式。", "錯誤");
                return default;
            }
            if (reply.Status != IPStatus.TtlExpired)
            {
                MessageBox.Show("找不到預設閘道 IP 位址，可能設備沒有連上網際網路，請確認後再開啟本程式。", "錯誤");
                return default;
            }
            return reply.Address;
        }

        public string GetGatewayMACAddress(string GatewayIP)
        {
            //呼叫 cmd 執行 arp -a 指令，透過IP找到ARP Table中MAC對應關係
            string dirResults = string.Empty;
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
                return MACAddress_Upper(m.ToString());
            }
            else
            {
                MessageBox.Show("找不到預設閘道 MAC 位址，ARP紀錄表查無該筆紀錄，請確認後再開啟本程式。", "錯誤");
                return "找不到預設閘道 MAC 位址";
            }
        }

        public static string GetRandomWifiMacAddress()
        {
            var random = new Random();
            var buffer = new byte[6];
            random.NextBytes(buffer);
            buffer[0] = 02;
            var result = string.Concat(buffer.Select(x => string.Format("{0}", x.ToString("X2"))).ToArray());
            return result;
        }

        public string MACAddress_Upper(string MACAddress)
        {
            //將MAC位址十六位元英文小寫轉大寫
            string English = "ABCDEF";
            string english = "abcdef";

            string a = MACAddress;
            string b = string.Empty;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] >= 'a' && a[i] <= 'f')
                {
                    for (int j = 0; j < 6; j++)
                    {
                        if (a[i] == english[j])
                        {
                            b += English[j];
                        }
                    }
                }
                else
                {
                    b += a[i];
                }
            }
            return b;
        }
    }
}
