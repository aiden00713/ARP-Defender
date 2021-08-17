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
        String testlable = string.Empty;
        public Form2()
        {
            InitializeComponent();
            label4.Text = GetHostMACAddress();
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
                                //do what...
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
            test_label2.Text = testlable;
            attackip.Text = attack_ip;
            attackmac.Text = attack_mac;
        }

        public string GetGatewayMACAddress(string GatewayIP)
        {
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
        }

        public string MACAddress_Upper(string MACAddress)
        {

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
