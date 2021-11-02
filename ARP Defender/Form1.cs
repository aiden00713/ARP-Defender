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
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;

namespace ARP_Defender
{
    public partial class Form1 : Form
    {
        Timer myTimer = new Timer();
        int Count = 0;
        int Freq = 300; //豪秒為單位，1秒執行3次 //500
        int check = 0;  //防禦
        public Form1()
        {
            InitializeComponent(); //初始化組件
        }

        string GatewayIPAddress = string.Empty;
        private void Form1_Load(object sender, EventArgs e)
        {
            GatewayIPAddress = GetGatewayIPAddress().ToString();

            hostip_label.Text = GetHostIPAddress();
            hostmac_label.Text = GetHostMACAddress();
            gatewayip_label.Text = GetGatewayIPAddress().ToString();
            gatewaymac_label.Text = GetGatewayMACAddress(GetGatewayIPAddress().ToString());

            if (gatewaymac_label.ToString() == "NOT Found！")
            {
                gatewaymac_label.Text = NOTFoundGatewayMACAddresses();
            }
            /*
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
            device.Open();*/
            //device.Filter = "arp"; //過濾ARP封包
            /* 當條件的封包被被截取時，執行 device_OnPacketArrival */
           /* device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);
            device.StartCapture();*/
        }

        /// <summary>
        /// Toolbar
        /// </summary>

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            notifyIcon1.Visible = true;
            this.Hide();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, EventArgs e)
        { 
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            notifyIcon1.ContextMenuStrip = contextMenuStrip1;
        }

        private void 結束程式ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String cmdstr = "delete neighbors" + " " + GetNetworkAdapterName() + " " + GetGatewayIPAddress().ToString();
            CMDARPdeletestatic(cmdstr);
            Environment.Exit(0); //徹底結束程式
        }

        private void 開啟程式ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        /// <summary>
        /// Main code
        /// </summary>

        private void start_Click(object sender, EventArgs e)
        {
            show_label.Text = "開啟偵測";
            show_label.ForeColor = Color.Green;
            start.Enabled = false;
            stop.Enabled = true;
            String cmdstr = "set neighbors" + " " + GetNetworkAdapterName() + " " + GatewayIPAddress + " " + GetGatewayMACAddress(GatewayIPAddress);
            CMDARPstatic(cmdstr);

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

            check = 0;
        }

        private void stop_Click(object sender, EventArgs e)
        {
            show_label.Text = "尚未開啟偵測";
            show_label.ForeColor = Color.Red;
            attack_mac_textBox.Text = "無";
            attack_ip_textBox.Text = "無";
            start.Enabled = true;
            stop.Enabled = false;
            show_attack_label.Text = "";
            String cmdstr = "delete neighbors" + " " + GetNetworkAdapterName() + " " + GatewayIPAddress;
            CMDARPdeletestatic(cmdstr);

            myTimer.Stop();

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
        }

        public string GetHostIPAddress()
        {
            //透過socket連線Google DNS判定該主機IP是否能正確連上網路
            string localIP = string.Empty;
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();

            return localIP;
        }

        public string GetHostMACAddress()
        {
            //從NetworkInterface找出正在使用的網卡
            Dictionary<string, long> macAddresses = new Dictionary<string, long>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses[nic.GetPhysicalAddress().ToString()] = nic.GetIPStatistics().BytesSent + nic.GetIPStatistics().BytesReceived;
                }    
            }
            long maxValue = 0;
            string mac = string.Empty;
            foreach (KeyValuePair<string, long> pair in macAddresses)
            {
                if (pair.Value > maxValue)
                {
                    mac = pair.Key;
                    maxValue = pair.Value;
                }
            }
            //格式表示
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1-$2-$3-$4-$5-$6";
            var mac_newformat = Regex.Replace(mac, regex, replace);
            return mac_newformat.ToString();
        }

        public static IPAddress GetGatewayIPAddress()          //public String GetGatewayIPAddress()
        {
            //透過ping Google DNS 達到traceroute效果，獲得主機預設閘道
            IPAddress netaddr = IPAddress.Parse("8.8.8.8");
            
            PingReply reply = default;
            var ping = new Ping();
            var options = new PingOptions(1, true); // ttl=1, don't fragment=true
            try
            {
                //200毫秒就 timeout
                reply = ping.Send(netaddr, 200, new byte[0], options);
            }
            catch (PingException)
            {
                MessageBox.Show("找不到預設閘道 IP 位址，可能設備沒有連上網際網路，請確認後再開啟本程式。", "錯誤");
                /*
                show_label.Text = "尚未開啟防禦";
                show_label.ForeColor = Color.Red;
                start.Enabled = true;
                stop.Enabled = false;
                myTimer.Stop();
                */
                return default;
            }
            if (reply.Status != IPStatus.TtlExpired)
            {
                MessageBox.Show("找不到預設閘道 IP 位址，可能設備沒有連上網際網路，請確認後再開啟本程式。", "錯誤");
                /*
                show_label.Text = "尚未開啟防禦";
                show_label.ForeColor = Color.Red;
                start.Enabled = true;
                stop.Enabled = false;
                myTimer.Stop();
                */
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
                return NOTFoundGatewayMACAddresses();
            }
        }

        public String NOTFoundGatewayMACAddresses()
        {
            //假如找不到Gateway 的IP與MAC對應，就從網卡找尋Gateway 的IP重新在ARP Table找對應

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (GatewayIPAddressInformation address in adapter.GetIPProperties().GatewayAddresses)
                    {
                        gatewayip_label.Text = address.Address.ToString();
                    }
                }          
            }

            string dirResults = string.Empty;
            ProcessStartInfo psi = new ProcessStartInfo();
            Process proc = new Process();
            psi.FileName = "arp";
            psi.RedirectStandardInput = false;
            psi.RedirectStandardOutput = true;
            psi.Arguments = "-a " + gatewayip_label.Text;
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
                return "NOT Found！";

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

        //要修改
        public EthernetPacket Send_ARPResponse_Packet()
        {
            string strEthDestMAC = GetGatewayMACAddress(GatewayIPAddress);
            string strEhSourMac = GetHostMACAddress();

            string strARPSourIP = GetHostIPAddress();
            string strARPSourMac = GetHostMACAddress();

            string strARPDestIP = GatewayIPAddress;
            string strARPDestMac = GetGatewayMACAddress(GatewayIPAddress);

            ArpPacket arp = new ArpPacket(ArpOperation.Response, PhysicalAddress.Parse(strARPDestMac), IPAddress.Parse(strARPDestIP), PhysicalAddress.Parse(strARPSourMac), IPAddress.Parse(strARPSourIP));
            EthernetPacket eth = new EthernetPacket(PhysicalAddress.Parse(strEhSourMac), PhysicalAddress.Parse(strEthDestMAC), EthernetType.Arp);
            eth.PayloadPacket = arp;
            return eth;
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
            try
            {
                var device = devices[i];
                device.Open();
                EthernetPacket eth = Send_ARPResponse_Packet();
                device.SendPacket(eth);
                Count++;
            }
            catch (Exception)
            {
                MessageBox.Show("無法正常防禦，請確認後再開啟本程式。", "錯誤");
                show_label.Text = "尚未開啟防禦";
                show_label.ForeColor = Color.Red;
                start.Enabled = true;
                stop.Enabled = false;
                myTimer.Stop();
            }
            
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

        /*偵測*/
        String attack_ip = string.Empty;
        String attack_mac = string.Empty;
        String show1 = string.Empty;
        String show2 = string.Empty;
        
        private void device_OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data); //封裝
                var arpPacket = packet.Extract<ArpPacket>(); //ARP封包

                if (arpPacket != null)
                {
                    MethodInvoker mi = new MethodInvoker(this.UpdateUI);
                    this.BeginInvoke(mi, null);

                    if (arpPacket.Operation.ToString() == "Request") //只收集請求封包
                    {
                        if (arpPacket.SenderProtocolAddress.ToString() == gatewayip_label.Text)
                        {
                            if (arpPacket.SenderHardwareAddress.ToString() != gatewaymac_label.Text)
                            {
                                attack_mac = arpPacket.SenderHardwareAddress.ToString();
                                attack_ip = GetARPIPaddress(attack_mac);
                                show1 = "有人正在竄改你的 ARP 對應！";
                                show2 = "開啟偵測與防禦";
                                check = 1;
                            }

                            if(check == 1)
                            {
                                myTimer.Tick += new EventHandler(SendPacket);
                                myTimer.Enabled = true;
                                myTimer.Interval = Freq;
                                check++;
                            }

                        }
                    }
                }
            }
            catch (Exception exce)
            {
                MessageBox.Show(exce.ToString(), "Error");
            }
        }

        private void UpdateUI()
        {
            attack_ip_textBox.Text += "\n" + attack_ip;
            attack_mac_textBox.Text += attack_mac + "\n";
            show_attack_label.Text = show1 ;
            show_label.Text = show2;
            show_label.ForeColor = Color.Blue;
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
                    dirResults = parts[0];
                }
            }
            catch (Exception) { }

            return dirResults;
        }
    }
}