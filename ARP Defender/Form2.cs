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
            device.Capture();
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
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1-$2-$3-$4-$5-$6";
            var mac_newformat = Regex.Replace(mac, regex, replace);
            return mac_newformat.ToString();
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
                    //var Packet = (ArpPacket)arpPacket.;

                    test_label.Text = arpPacket.Operation.ToString();

                    /*if (Packet.Operation.ToString() == "01") //只收集請求封包
                    {
                        //PhysicalAddress Eth_SenderMAC = 
                        IPAddress SenderIP = Packet.SenderProtocolAddress;
                        PhysicalAddress SenderMAC = Packet.SenderHardwareAddress;

                        if (SenderIP == IPAddress.Parse(GetHostIPAddress()) && SenderMAC != PhysicalAddress.Parse(GetHostMACAddress()))
                        {
                            attackmac.Text = SenderMAC.ToString();
                        }
                    }*/
                }
            }
            catch(Exception)
            {
                MessageBox.Show("Error", "Error");
            }
        }


    }
}
