using MySql.Data.MySqlClient;
using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace smtvoiceSever
{
    public partial class Form1 : Form
    {
        IPEndPoint ip;
        Socket server;
        Thread thread1;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;

            listViewUser.FullRowSelect = true;//要选择就是一行
            listViewUser.Columns.Add("UserID", 120, HorizontalAlignment.Center);
            listViewUser.Columns.Add("DeviceID", 100, HorizontalAlignment.Center);
            listViewUser.Columns.Add("IP", 160, HorizontalAlignment.Center);
            listViewUser.Columns.Add("ResponseData", 450, HorizontalAlignment.Center);
            //ListViewItem lvi1 = Helper.listAdd("备用", "456789", "192");
            //listViewUser.Items.Add(lvi1);
            //ListViewItem lvi = Helper.GetItem(listViewUser, "456789", 1);
            //textBox3.Text = lvi.SubItems[3].Text.Length.ToString();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //存储配置
            string configStr;
            configStr = Ini.Read("配置", "TimeOut");
            if (configStr != "null")
                textBoxTimeOut.Text = configStr;
            configStr = Ini.Read("配置", "IP");
            if (configStr != "null")
                textBox1.Text = configStr;
            configStr = Ini.Read("配置", "Port");
            if (configStr != "null")
                textBox2.Text = configStr;
        }

        public void addText(string str)
        {
            if (checkBox1.CheckState == CheckState.Checked)
            {
                textBox3.AppendText(str);     // 追加文本，并且使得光标定位到插入地方。
                textBox3.ScrollToCaret();
            }
        }

        //启动TCP服务器
        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "启动")
            {

                //通讯设置失效
                textBox1.Enabled = false;
                textBox2.Enabled = false;
                //按钮功能变化
                button1.Text = "停止";
                button1.ForeColor = Color.Red;


                ip = new IPEndPoint(IPAddress.Parse(textBox1.Text), int.Parse(textBox2.Text));
                server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                server.Bind(ip);
                addText("启动监听\n");
                thread1 = new Thread(th1);
                thread1.Start();

                ////启动检测连接进程
                //threadCheckConnet = new Thread(CheckConnet);
                //threadCheckConnet.IsBackground = true;
                //threadCheckConnet.Start();
            }
            else
            {
                //通讯设置恢复
                textBox1.Enabled = true;
                textBox2.Enabled = true;
                //按钮功能变化
                button1.Text = "启动";
                button1.ForeColor = Color.Green;
                // con.ServerStop();
                thread1.Abort();
                listViewUser.Items.Clear();
                server.Close();
                server.Dispose();
                addText("停止服务\r\n");
                //con.ServerStop();
                //  threadCheckConnet.Abort();
            }
        }
        //清空数据
        private void button3_Click(object sender, EventArgs e)
        {
            textBox3.Clear();
        }

        struct pointData
        {
            public EndPoint remote;
            public byte[] recv;
            public int length;
        };

        //启动监听
        private void th1()
        {
            byte[] bytes = new byte[1024];
            pointData pd = new pointData();
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint Remote = (EndPoint)(sender);
            while (true)
            {
                try
                {
                    Thread.Sleep(10);
                    addText("主线程开始\r\n\r\n");
                    bytes = new byte[1000];
                    pd = new pointData();
                    pd.length = server.ReceiveFrom(bytes, ref Remote);
                    addText("主线程接收到了一组数据\r\n\r\n");
                    pd.remote = Remote;
                    pd.recv = bytes;
                    //收到数据后启用一个新的线程处理数据
                    Thread clientThread = new Thread(new ParameterizedThreadStart(ThreadFunc));
                    clientThread.IsBackground = true;
                    clientThread.Start(pd);
                    label5.Text = string.Format("当前连接数:{0}",listViewUser.Items.Count);
                }
                catch 
                {
                    // addText(ex.ToString());
                }
            }

        }

        //功能函数
        private void ThreadFunc(object obj)
        {
            int index = 0;
            try
            {
                Thread.Sleep(10);
                byte[] bytes = new byte[1024];
                pointData pd = (pointData)obj;
                addText(string.Format("子线程收到数据： {0}\r\n", pd.remote.ToString()));
                string str = Encoding.GetEncoding("GB2312").GetString(pd.recv, 0, pd.length);
                addText(string.Format("Message: {0}\r\n", str));
                DeviceHelper dh = new DeviceHelper();
                //反序列化
                dh = Helper.GetJson(str);

                //获取设备信息
                ListViewItem lvi = Helper.GetItem(listViewUser, dh.userid, dh.deviceid);
                addText("userid:" + dh.userid + "\r\n");

                //获取设备index
                index = Helper.GetIndex(listViewUser, dh.userid, dh.deviceid);
                addText("index:" + index.ToString() + "\r\n");
                if (dh.type != null)
                {
                    switch (dh.type)
                    {
                        case "identity":
                            #region 硬件IP绑定
                            addText("identity\r\n");
                            //判断数据库中是否存在该用户ID
                            if (true)
                            {
                                //查看是否已存在该ID号
                                if (index >= 0)
                                {
                                    //直接删除
                                    listViewUser.Items.RemoveAt(index);
                                }
                                //添加
                                lvi = new ListViewItem();
                                lvi = Helper.listAdd(dh.userid, dh.deviceid, pd.remote.ToString());
                                listViewUser.Items.Add(lvi);

                                bytes = Encoding.GetEncoding("GB2312").GetBytes("OK");
                                server.SendTo(bytes, pd.remote);
                            }
                            //else
                            //{
                            //    bytes = Encoding.GetEncoding("GB2312").GetBytes("ID号不正确！");
                            //    server.SendTo(bytes, pd.remote);
                            //}
                            break;
                        #endregion
                        case "set":
                            #region 网路端控制硬件请求
                            addText("set\r\n");
                            //将请求转发到对应的设备IP地址
                            dh.type = "response";
                            if (index < 0)
                            {

                                dh.state = "设备不在线！";
                                //返回设备不在线
                                bytes = Encoding.GetEncoding("GB2312").GetBytes(Helper.SetJson(dh));
                                server.SendTo(bytes, pd.remote);
                            }
                            else
                            {
                                //将数据列表的数据清空，防止设备端误操作
                                listViewUser.Items[lvi.Index].SubItems[3].Text = string.Empty;
                                //将数据转发到设备端
                                try
                                {
                                    sendToUdp(lvi.SubItems[2].Text, str);
                                    //等待返回数据，10ms查一次，共等待5s
                                    int tNum = 0;
                                    while (tNum < int.Parse(textBoxTimeOut.Text))
                                    {
                                        Thread.Sleep(10);
                                       // addText(tNum.ToString() + "\r\n");
                                        tNum++;
                                        //查看是否有数据
                                        lvi = new ListViewItem();
                                        lvi = Helper.GetItem(listViewUser, dh.userid,dh.deviceid);
                                        //addText(lvi.SubItems[3].Text.Length.ToString() + "\r\n");
                                        if (lvi.SubItems[3].Text.Length > 0)//有返回数据
                                        {
                                            sendToUdp(pd.remote, lvi.SubItems[3].Text);
                                            addText("给网络端回复:" + pd.remote + "\r\n");
                                            addText(lvi.SubItems[3].Text + "\r\n");
                                            //清空数据
                                            listViewUser.Items[lvi.Index].SubItems[3].Text = string.Empty;
                                            //Thread.Sleep(10);
                                            //给设备返回OK
                                            //sendToUdp(lvi.SubItems[2].Text, "OK");
                                            //addText("OK\r\n");
                                            tNum = 501;
                                            Thread.Sleep(10);
                                        }
                                    }
                                    if (tNum == 200)//超时
                                    {
                                        //给网络返回超时
                                        //超时
                                        dh.state = "设备响应超时！";
                                        bytes = Encoding.GetEncoding("GB2312").GetBytes(Helper.SetJson(dh));
                                        server.SendTo(bytes, pd.remote);
                                        addText("设备响应超时！\r\n");
                                    }
                                }
                                catch
                                {
                                    dh.state = "设备与服务器连接失效！";
                                    sendToUdp(pd.remote, Helper.SetJson(dh));
                                }
                            }
                            break;
                        #endregion
                        case "get":
                            #region 网络端获取设备状态
                            addText("get\r\n");
                            //将请求转发到对应的设备IP地址
                            dh.type = "response";
                            if (index < 0)
                            {

                                dh.state = "设备不在线！";
                                //返回设备不在线
                                bytes = Encoding.GetEncoding("GB2312").GetBytes(Helper.SetJson(dh));
                                server.SendTo(bytes, pd.remote);
                            }
                            else
                            {
                                //将数据列表的数据清空，防止设备端误操作
                                listViewUser.Items[lvi.Index].SubItems[3].Text = string.Empty;
                                //将数据转发到设备端
                                try
                                {
                                    sendToUdp(lvi.SubItems[2].Text, str);
                                    //等待返回数据，10ms查一次，共等待5s
                                    int tNum = 0;
                                    while (tNum < int.Parse(textBoxTimeOut.Text))
                                    {
                                        Thread.Sleep(10);
                                        addText(tNum.ToString() + "\r\n");
                                        tNum++;
                                        //查看是否有数据
                                        lvi = new ListViewItem();
                                        lvi = Helper.GetItem(listViewUser, dh.userid,dh.deviceid);
                                        addText(lvi.SubItems[3].Text.Length.ToString() + "\r\n");
                                        if (lvi.SubItems[3].Text.Length > 0)//有返回数据
                                        {
                                            sendToUdp(pd.remote, lvi.SubItems[3].Text);
                                            addText("给网络端回复:" + pd.remote + "\r\n");
                                            addText(lvi.SubItems[3].Text + "\r\n");
                                            //清空数据
                                            listViewUser.Items[lvi.Index].SubItems[3].Text = string.Empty;
                                            //Thread.Sleep(10);
                                            //给设备返回OK
                                            //sendToUdp(lvi.SubItems[2].Text, "OK");
                                            //addText("OK\r\n");
                                            tNum = 501;
                                            Thread.Sleep(10);
                                        }
                                    }
                                    if (tNum == int.Parse(textBoxTimeOut.Text))//超时
                                    {
                                        //给网络返回超时
                                        //超时
                                        dh.state = "设备响应超时！";
                                        bytes = Encoding.GetEncoding("GB2312").GetBytes(Helper.SetJson(dh));
                                        server.SendTo(bytes, pd.remote);
                                        addText("设备响应超时！\r\n");
                                    }
                                }
                                catch
                                {
                                    dh.state = "设备与服务器连接失效！";
                                    sendToUdp(pd.remote, Helper.SetJson(dh));
                                }
                            }
                            break;
                        #endregion
                        case "response":
                            #region 设备端响应网络请求
                            addText("response\r\n");
                            //查看是否应经绑定到列表
                            int i = Helper.GetIndex(listViewUser, dh.userid,dh.deviceid);
                            if (i >= 0)
                            {
                                //写入数据
                                listViewUser.Items[i].SubItems[3].Text = str;
                                //给设备返回OK
                                sendToUdp(pd.remote, "ok");
                                addText("ok\r\n");
                            }
                            else
                            {
                                sendToUdp(pd.remote, "error");
                                addText("error\r\n");
                            }
                            break;
                        #endregion
                        case "upload":
                            #region 设备上传数据(传感器)
                            addText("upload\r\n");

                            //判断数据库中是否存在该用户ID和设备ID
                            if (true)
                            {
                                //存入数据库
                                userUpdate(dh.userid, dh.deviceid, dh.state);

                                bytes = Encoding.GetEncoding("GB2312").GetBytes("ok");
                                server.SendTo(bytes, pd.remote);
                            }
                            break;
                        #endregion
                        default:
                            addText("default\r\n");
                            bytes = Encoding.GetEncoding("GB2312").GetBytes("无效指令！");
                            server.SendTo(bytes, pd.remote);
                            break;
                    }
                }
                else
                {
                    server.SendTo(Encoding.GetEncoding("GB2312").GetBytes("数据格式不正确！\r\n" + "详细说明请登录www.geek-iot.com"), pd.remote);
                }
            }
            catch (Exception ex)
            {
                addText(ex.ToString() + "\r\n");
            }
            addText("执行完成\r\n\r\n");
        }

        //发送数据
        private void button2_Click(object sender, EventArgs e)
        {
            if (textBox8.Text != string.Empty && textBox5.Text != string.Empty)
                sendToUdp(textBox8.Text, textBox5.Text);
        }
        /// <summary>
        /// 发送到目标ip
        /// </summary>
        /// <param name="DstIpPort">目标ip及端口 例："192.168.1.1:8080"</param>
        /// <param name="str">待发送内容</param>
        public void sendToUdp(string DstIpPort, string str)
        {
            byte[] bytes = new byte[1024];
            ip = new IPEndPoint(IPAddress.Parse(DstIpPort.Substring(0, DstIpPort.IndexOf(":"))), int.Parse(DstIpPort.Substring(DstIpPort.IndexOf(":") + 1)));
            try
            {
                if (str != null && DstIpPort != null)
                {
                    bytes = Encoding.GetEncoding("GB2312").GetBytes(str);
                    server.SendTo(bytes, ip);
                }
                else
                {
                    MessageBox.Show("请选择目标客户端");
                }
            }
            catch
            { }
        }
        public void sendToUdp(string DstIpPort, byte[] str)
        {
            ip = new IPEndPoint(IPAddress.Parse(DstIpPort.Substring(0, DstIpPort.IndexOf(":"))), int.Parse(DstIpPort.Substring(DstIpPort.IndexOf(":") + 1)));
            try
            {
                if (DstIpPort != null)
                {
                    server.SendTo(str, ip);
                }
                else
                {
                    MessageBox.Show("请选择目标客户端");
                }
            }
            catch
            { }
        }
        /// <summary>
        /// 发送到目标ip
        /// </summary>
        /// <param name="EndPort">EndPoint</param>
        /// <param name="str">byte[]</param>
        public void sendToUdp(EndPoint EndPort, byte[] str)
        {
            try
            {
                if (str != null && EndPort.ToString() != null)
                {
                    server.SendTo(str, EndPort);
                }
                else
                {
                    MessageBox.Show("请选择目标客户端");
                }
            }
            catch
            { }
        }
        /// <summary>
        /// 发送到目标ip
        /// </summary>
        /// <param name="EndPort"></param>
        /// <param name="str">待发送字符串</param>
        public void sendToUdp(EndPoint EndPort, string str)
        {
            byte[] bytes = new byte[1024];
            try
            {
                if (str != null && EndPort.ToString() != null)
                {
                    bytes = Encoding.GetEncoding("GB2312").GetBytes(str);
                    server.SendTo(bytes, EndPort);
                }
                else
                {
                    MessageBox.Show("请选择目标客户端");
                }
            }
            catch
            { }
        }

        private void listViewUser_MouseClick(object sender, MouseEventArgs e)
        {
            textBox8.Text = listViewUser.SelectedItems[0].SubItems[2].Text;
        }




        #region 获取本机IP
        // 尝试Ping指定IP是否能够Ping通
        public static bool IsPingIP(string strIP)
        {
            try
            {
                //创建Ping对象
                Ping ping = new Ping();
                //接受Ping返回值
                PingReply reply = ping.Send(strIP, 1000);
                //Ping通
                return true;
            }
            catch
            {
                //Ping失败
                return false;
            }
        }
        //得到网关地址
        public static string GetGateway()
        {
            //网关地址
            string strGateway = "";
            //获取所有网卡
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            //遍历数组
            foreach (var netWork in nics)
            {
                //单个网卡的IP对象
                IPInterfaceProperties ip = netWork.GetIPProperties();
                //获取该IP对象的网关
                GatewayIPAddressInformationCollection gateways = ip.GatewayAddresses;
                foreach (var gateWay in gateways)
                {
                    //如果能够Ping通网关
                    if (IsPingIP(gateWay.Address.ToString()))
                    {
                        //得到网关地址
                        strGateway = gateWay.Address.ToString();
                        //跳出循环
                        break;
                    }
                }
                //如果已经得到网关地址
                if (strGateway.Length > 0)
                {
                    //跳出循环
                    break;
                }
            }
            //返回网关地址
            return strGateway;
        }
        //得到IP地址
        public static string GetIp()
        {
            string IPname = "";
            try
            {
                string name = Dns.GetHostName();
                IPAddress[] ips;
                ips = Dns.GetHostAddresses(name);

                string temp = GetGateway();
                string gateway = string.Empty;
                int num = 0;
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp.Substring(i, 1) == ".")
                    {
                        num += 1;
                        if (num == 3)
                            i = temp.Length;
                    }
                    if (num != 3)
                        gateway += temp.Substring(i, 1);
                }
                for (int i = 0; i < ips.Length; i++)
                {
                    if (ips[i].ToString().StartsWith(gateway))
                    {
                        IPname += ips[i];
                        i = ips.Length;
                    }
                }
            }
            catch
            { }
            return IPname;
        }
        #endregion

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            System.Environment.Exit(System.Environment.ExitCode);
        }

        //mysql



        /// <summary>
        /// 建立mysql数据库链接
        /// </summary>
        /// <returns></returns>
        public static MySqlConnection getMySqlCon()
        {
            String mysqlStr = "Database=web;Data Source=127.0.0.1;User Id=root;Password=sun8008208820;pooling=false;CharSet=utf8;port=3306";
            // String mySqlCon = ConfigurationManager.ConnectionStrings["MySqlCon"].ConnectionString;
            MySqlConnection mysql = new MySqlConnection(mysqlStr);
            return mysql;
        }
        /// <summary>
        /// 建立执行命令语句对象
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="mysql"></param>
        /// <returns></returns>
        public static MySqlCommand getSqlCommand(String sql, MySqlConnection mysql)
        {
            MySqlCommand mySqlCommand = new MySqlCommand(sql, mysql);
            //  MySqlCommand mySqlCommand = new MySqlCommand(sql);
            // mySqlCommand.Connection = mysql;
            return mySqlCommand;
        }
        /// <summary>
        /// 查询并获得结果集并遍历
        /// </summary>
        /// <param name="mySqlCommand"></param>
        public static void getResultset(MySqlCommand mySqlCommand)
        {
            MySqlDataReader reader = mySqlCommand.ExecuteReader();
            try
            {
                string rs = string.Empty;
                while (reader.Read())
                {
                    if (reader.HasRows)
                    {
                        rs += reader.GetInt32(0) + "  ";
                        for (int i = 1; i < reader.FieldCount; i++)
                            rs += reader.GetString(i) + "  ";
                        rs += "\r\n";
                    }
                }
                MessageBox.Show(rs);
            }
            catch (Exception)
            {

                Console.WriteLine("查询失败了！");
            }
            finally
            {
                reader.Close();
            }
        }
        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="mySqlCommand"></param>
        public static void getInsert(MySqlCommand mySqlCommand)
        {
            try
            {
                mySqlCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                String message = ex.Message;
                MessageBox.Show(message);
                Console.WriteLine("插入数据失败了！" + message);
            }

        }
        /// <summary>
        /// 修改数据
        /// </summary>
        /// <param name="mySqlCommand"></param>
        public static void getUpdate(MySqlCommand mySqlCommand)
        {
            try
            {
                mySqlCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {

                String message = ex.Message;
                Console.WriteLine("修改数据失败了！" + message);
            }
        }
        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="mySqlCommand"></param>
        public static void getDel(MySqlCommand mySqlCommand)
        {
            try
            {
                mySqlCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                String message = ex.Message;
                Console.WriteLine("删除数据失败了！" + message);
            }
        }

        //用户上传数据
        private void userUpdate(string userid, string sensorid, string data)
        {
            try
            {
                //创建数据库连接对象
                MySqlConnection mysql = getMySqlCon();

                //打开数据库
                mysql.Open();

                //查询用户的设备id是否存在
                String sqlSearch = "select * from sensor where userid='" + userid + "' and id = '" + sensorid + "'";
                MySqlCommand mySqlCommand = getSqlCommand(sqlSearch, mysql);

                MySqlDataReader reader = mySqlCommand.ExecuteReader();

                if (reader.Read())//存在，更新数据，不存在不处理
                {
                    reader.Close();
                    //修改sql
                    String sqlUpdate = "update sensor set data='" + data + "' where userid='" + userid + "' and id = '" + sensorid + "'";
                    mySqlCommand = getSqlCommand(sqlUpdate, mysql);
                    mySqlCommand.ExecuteNonQuery();
                }
                //关闭
                mysql.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void textBoxTimeOut_TextChanged(object sender, EventArgs e)
        {
            //存储配置
            Ini.Write("配置", "TimeOut", textBoxTimeOut.Text);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            //存储配置
            Ini.Write("配置", "IP", textBox1.Text);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            //存储配置
            Ini.Write("配置", "Port", textBox2.Text);
        }
    }

    /// <summary>
    /// 通信协议类
    /// </summary>
    public class DeviceHelper
    {
        public string type;//消息类型
        public string userid;//用户ID
        public string deviceid;//设备ID
        public string state;   //设备状态
    }


    public class Helper
    {
        /// <summary>
        /// 转对象到JSON字符串
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="deviceid"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string SetJson(string type, string userid, string deviceid, string state)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            //js.Serialize();
            DeviceHelper info = new DeviceHelper();
            info.type = type;
            info.userid = userid;
            info.deviceid = deviceid;
            info.state = state;
            //转为json字符串
            string dd = js.Serialize(info);
            return dd;
        }
        /// <summary>
        /// 转对象到JSON字符串
        /// </summary>
        /// <param name="deviceHelper">DeviceHelper</param>
        /// <returns></returns>
        public static string SetJson(DeviceHelper deviceHelper)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            //转为json字符串
            string dd = js.Serialize(deviceHelper);
            return dd;
        }
        /// <summary>
        /// 转JSON字符串为对象
        /// </summary>
        /// <param name="src">json格式的字符串</param>
        /// <returns></returns>
        public static DeviceHelper GetJson(string src)
        {
            DeviceHelper inf;
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                inf = new DeviceHelper();
                inf = js.Deserialize<DeviceHelper>(src);
                return inf;
            }
            catch
            {
                inf = new DeviceHelper();
                return inf;
            }
        }

        /// <summary>
        /// 获取记录索引
        /// </summary>
        /// <param name="listview">待搜索的数据列表</param>
        /// <param name="keyValue">需要搜索的关键值，默认是第0个值</param>
        /// <returns></returns>

        public static int GetIndex(ListView listview, string userid, string deviceid)
        {
            ListView lv = new ListView();
            ListViewItem li;
            try
            {
                li = listview.Items.Cast<ListViewItem>().First(x => (x.Text == userid && x.SubItems[1].Text == deviceid));
                return li.Index;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 获取记录索引
        /// </summary>
        /// <param name="listview"></param>
        /// <param name="keyValue"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        //public static int GetIndex(ListView listview, string keyValue, int index = 0)
        //{
        //    ListViewItem li;
        //    try
        //    {
        //        if (index == 0)
        //        {
        //            li = listview.Items.Cast<ListViewItem>().First(x => x.Text == keyValue);
        //        }
        //        else
        //        {
        //            li = listview.Items.Cast<ListViewItem>().First(x => x.SubItems[index].Text == keyValue);
        //        }
        //        return li.Index;
        //    }
        //    catch
        //    {
        //        return -1;
        //    }
        //}

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="listview">待搜索的数据列表</param>
        /// <param name="keyValue">需要搜索的关键值，默认是第0个值</param>
        /// <returns></returns>

        public static ListViewItem GetItem(ListView listview, string userid, string deviceid)
        {
            ListViewItem li;
            try
            {
                li = listview.Items.Cast<ListViewItem>().First(x => x.Text == userid && x.SubItems[1].Text == deviceid);
                return li;
            }
            catch
            {
                li = new ListViewItem();
                return li;
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="listview"></param>
        /// <param name="keyValue"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        //public static ListViewItem GetItem(ListView listview, string keyValue, int index = 0)
        //{
        //    ListViewItem li;
        //    try
        //    {
        //        if (index == 0)
        //        {
        //            li = listview.Items.Cast<ListViewItem>().First(x => x.Text == keyValue);
        //        }
        //        else
        //        {
        //            li = listview.Items.Cast<ListViewItem>().First(x => x.SubItems[index].Text == keyValue);
        //        }
        //        return li;
        //    }
        //    catch
        //    {
        //        li = new ListViewItem();
        //        return li;
        //    }
        //}


        //增加记录


        public static ListViewItem listAdd(string userid, string deviceid, string IP, string responseData = "")
        {
            ListViewItem item = new ListViewItem();
            item.Text = userid;
            item.SubItems.Add(deviceid);
            item.SubItems.Add(IP);
            item.SubItems.Add(responseData);
            return item;
        }
    }
}
