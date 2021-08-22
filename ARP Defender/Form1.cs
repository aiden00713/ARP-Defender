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
        public Form1()
        {
            InitializeComponent(); //初始化組件
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            hostip_label.Text = GetHostIPAddress();
            hostmac_label.Text = GetHostMACAddress();
            gatewayip_label.Text = GetGatewayIPAddress().ToString();
            gatewaymac_label.Text = GetGatewayMACAddress(GetGatewayIPAddress().ToString());

            show_label.Text = "開啟防禦";
            show_label.ForeColor = Color.Green;
            start.Enabled = false;
            stop.Enabled = true;
            String cmdstr = "set neighbors" + " " + GetNetworkAdapterName() + " " + GetGatewayIPAddress().ToString() + " " + GetGatewayMACAddress(GetGatewayIPAddress().ToString());
            CMDARPstatic(cmdstr);

            Count = 0;
            myTimer.Tick += new EventHandler(SendPacket);
            myTimer.Enabled = true;
            myTimer.Interval = 500; //豪秒為單位，1秒執行2次
            //test_label.Text = ;
            //test_label.Text = "set neighbors" + " " + GetNetworkAdapterName() + " " + GetGatewayIPAddress().ToString() + " " + GetGatewayMACAddress(GetGatewayIPAddress().ToString()); 
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
            show_label.Text = "開啟防禦";
            show_label.ForeColor = Color.Green;
            start.Enabled = false;
            stop.Enabled = true;
            String cmdstr = "set neighbors" + " " + GetNetworkAdapterName() + " " + GetGatewayIPAddress().ToString() + " " + GetGatewayMACAddress(GetGatewayIPAddress().ToString());
            CMDARPstatic(cmdstr);

            Count = 0;
            myTimer.Tick += new EventHandler(SendPacket);
            myTimer.Enabled = true;
            myTimer.Interval = 500; //豪秒為單位，1秒執行4次
        }

        private void stop_Click(object sender, EventArgs e)
        {
            show_label.Text = "尚未開啟防禦";
            show_label.ForeColor = Color.Red;
            start.Enabled = true;
            stop.Enabled = false;
            String cmdstr = "delete neighbors" + " " + GetNetworkAdapterName() + " " + GetGatewayIPAddress().ToString();
            CMDARPdeletestatic(cmdstr);

            myTimer.Stop();
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

        public EthernetPacket Send_ARPResponse_Packet()
        {
            string strEthDestMAC = GetGatewayMACAddress(GetGatewayIPAddress().ToString());
            string strEhSourMac = GetHostMACAddress();

            string strARPSourIP = GetHostIPAddress();
            string strARPSourMac = GetHostMACAddress();

            string strARPDestIP = GetGatewayIPAddress().ToString();
            string strARPDestMac = GetGatewayMACAddress(GetGatewayIPAddress().ToString());

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
            var device = devices[i];
            device.Open();
            EthernetPacket eth = Send_ARPResponse_Packet();
            device.SendPacket(eth);
            Count++;
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

        private void whocutme_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2(); //創建子視窗
            form2.Show();
        }
    }
}