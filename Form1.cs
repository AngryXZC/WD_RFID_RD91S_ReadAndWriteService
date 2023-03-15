using LitJson;
using RFIDService.Models;
using RFIDService.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFIDService
{
    public partial class Form1 : Form
    {
        SingleDevice singleDevice = null;
        HttpListener httpobj=null;
        public Form1()
        {
            InitializeComponent();
            singleDevice = SingleDevice.getInstance();
            this.Load += new System.EventHandler(this.Form1_Load);

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            notifyIcon1.Icon = new Icon("icon1.ico");
            notifyIcon1.Visible = false;
            notifyIcon1.Click += new System.EventHandler(this.notifyIcon1_Click);
            //this.SizeChanged += new System.EventHandler(this.MainWnd_SizeChanged);
            //自动最小化，因为添加了notifyIcon，最小化时不会最小化到任务栏，而是到托盘　　　　
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = true;//任务栏里不显示任务
            this.notifyIcon1.Visible = true;//这个必须为true



            this.FormClosing += Form1_FormClosing;
            singleDevice.connect();
            Thread.Sleep(1000);
            singleDevice.startInventoryReal();
            httpobj = new HttpListener();
            //定义url及端口号，通常设置为配置文件
            INIParser ini = new INIParser();
            ini.Open(@"config.ini");
            string urlAndPort = ini.ReadValue("Address", "address", "NULL");
            ini.Close();
            httpobj.Prefixes.Add(urlAndPort);
            //启动监听器
            httpobj.Start();
            //异步监听客户端请求，当客户端的网络请求到来时会自动执行Result委托
            //该委托没有返回值，有一个IAsyncResult接口的参数，可通过该参数获取context对象
            httpobj.BeginGetContext(Result, null);
            Console.WriteLine($"服务端初始化完毕，正在等待客户端请求,时间：{DateTime.Now.ToString()}\r\n");
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            
                singleDevice.stopInventory();

                singleDevice.disconnect();
            MessageBox.Show("即将退出应用！");
        }


        private  void Result(IAsyncResult ar)
        {
            //当接收到请求后程序流会走到这里

            //继续异步监听
            httpobj.BeginGetContext(Result, null);
            var guid = Guid.NewGuid().ToString();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"接到新的请求:{guid},时间：{DateTime.Now.ToString()}");
            //获得context对象
            var context = httpobj.EndGetContext(ar);
            var request = context.Request;
            var response = context.Response;
            ////如果是js的ajax请求，还可以设置跨域的ip地址与参数
            //context.Response.AppendHeader("Access-Control-Allow-Origin", "*");//后台跨域请求，通常设置为配置文件
            //context.Response.AppendHeader("Access-Control-Allow-Headers", "ID,PW");//后台跨域参数设置，通常设置为配置文件
            //context.Response.AppendHeader("Access-Control-Allow-Method", "post");//后台跨域请求设置，通常设置为配置文件
            context.Response.ContentType = "text/plain;charset=UTF-8";//告诉客户端返回的ContentType类型为纯文本格式，编码为UTF-8
            context.Response.AddHeader("Content-type", "text/plain");//添加响应头信息
            context.Response.ContentEncoding = Encoding.UTF8;
            string returnObj = null;//定义返回客户端的信息
            if (request.HttpMethod == "POST" && request.InputStream != null)
            {
                //处理客户端发送的请求并返回处理信息
                returnObj = HandleRequest(request, response);
            }
            else
            {
                returnObj = $"不是post请求或者传过来的数据为空";
            }
            var returnByteArr = Encoding.UTF8.GetBytes(returnObj);//设置客户端返回信息的编码
            try
            {
                using (var stream = response.OutputStream)
                {
                    //把处理信息返回到客户端
                    stream.Write(returnByteArr, 0, returnByteArr.Length);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"网络蹦了：{ex.ToString()}");
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"请求处理完成：{guid},时间：{ DateTime.Now.ToString()}\r\n");
        }




        private  string HandleRequest(HttpListenerRequest request, HttpListenerResponse response)
        {

            Console.WriteLine($"HTTP method: {request.RawUrl}");
            string data = extractData(request, response);
            OperateModel operate = JsonMapper.ToObject<OperateModel>(data);
            switch (request.RawUrl)
            {
                case "/write":
                    
                    Root<OperateModel> res = new Root<OperateModel>();
                    res.result = operate;
                    writeATag(operate.toBeWriteTag,operate.targetTag, res);
                    //Thread.Sleep(10000);
                    //singleDevice.disconnect();
                    string result = JsonMapper.ToJson(res);
                    return result;
                case "/read":
                   
                    res = new Root<OperateModel>();
                    res.result = operate;
                    readATag(res);
                    Thread.Sleep(500);
                    readATag(res);
                    result = JsonMapper.ToJson(res);
                    return result;
                default:
                    break;
            }


            response.StatusDescription = "200";//获取或设置返回给客户端的 HTTP 状态代码的文本说明。
            response.StatusCode = 200;// 获取或设置返回给客户端的 HTTP 状态代码。
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"接收数据完成:{data.Trim()},时间：{DateTime.Now.ToString()}");
            //获取得到数据data可以进行其他操作

            return $"接收数据完成";
        }

        /// <summary>
        /// 提取请求数据
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private static string extractData(HttpListenerRequest request, HttpListenerResponse response)
        {
            string data = null;
            try
            {
                var byteList = new List<byte>();
                var byteArr = new byte[2048];
                int readLen = 0;
                int len = 0;
                //接收客户端传过来的数据并转成字符串类型
                do
                {
                    readLen = request.InputStream.Read(byteArr, 0, byteArr.Length);
                    len += readLen;
                    byteList.AddRange(byteArr);
                } while (readLen != 0);
                data = Encoding.UTF8.GetString(byteList.ToArray(), 0, len);

                return data;
            }
            catch (Exception ex)
            {
                response.StatusDescription = "404";
                response.StatusCode = 404;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"在接收数据时发生错误:{ex.ToString()}");
                return $"在接收数据时发生错误:{ex.ToString()}";//把服务端错误信息直接返回可能会导致信息不安全，此处仅供参考
            }
        }


        /// <summary>
        /// 写标签
        /// </summary>
        /// <param name="targetTag"> 欲修改的标签</param>
        /// <param name="toBeWriteTag">写入编号</param>
        /// <param name="root"></param>
        private void writeATag(string toBeWriteTag, string targetTag, Root<OperateModel> root)
        {

            //singleDevice.connect();
            if (!singleDevice.isLoop) 
                singleDevice.startInventoryReal();
            root.result.currentTag = "";
            if ((!singleDevice.IsConnected))
            {
                root.message = ("请检查设备连接！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
            if ((singleDevice.WriteTag is null))
            {
                root.message = ("读写器当前未读到任何标签！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
            if (toBeWriteTag.Length != 16)
            {
                root.message = ("请输入16位样品编码！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
            if ((root.result.toBeWriteTag is null)||( root.result.toBeWriteTag=="")) 
            {
                root.message = ("请输入要操作的标签！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
            else
            {

                singleDevice.writeCurrentTag(toBeWriteTag, targetTag);
                Thread.Sleep(500);
                root.result.currentTag = singleDevice.WriteTag;
                root.message = ("成功！");
                root.code = 200;
                root.result.resultFlag = true;
                if (!(root.result.currentTag is null))
                {
                    string tempCode=root.result.currentTag.Replace(" ","");
                    tempCode = tempCode.Substring(0, 16);
                    if (tempCode.Equals(root.result.toBeWriteTag)) { root.result.resultFlag = true; }
                    else { root.result.resultFlag = false; }
                    
                }
                else {
                    root.result.resultFlag = false;
                }
              
            }
        }



        /// <summary>
        /// 读一个标签
        /// </summary>
        
        /// <param name="root"></param>
        private void readATag( Root<OperateModel> root)
        {
           
            if (!singleDevice.isLoop)
                singleDevice.startInventoryReal();
            root.result.currentTag = "";
            if ((!singleDevice.IsConnected))
            {
                root.message = ("请检查设备连接！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
            if ((singleDevice.WriteTag is null))
            {
                root.message = ("读写器当前未读到任何标签！");
                root.code = 200;
                root.result.resultFlag = false;
                root.success = true;
                return;
            }
           
            else
            {
                
                root.result.currentTag = singleDevice.WriteTag;
                singleDevice.WriteTag = null;
                root.message = ("成功！");
                root.code = 200;
                root.result.resultFlag = true;
                root.success = true;
            }
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.notifyIcon1.Visible = false;
            this.ShowInTaskbar = true;
        }
        /// <summary>
        /// 重连
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pictureBox1_DoubleClick(object sender, EventArgs e)
        {
            DialogResult dr= MessageBox.Show("即将重新连接RFID读卡是否继续？","重连提示",MessageBoxButtons.OKCancel,MessageBoxIcon.Information);
            if (dr==DialogResult.OK) {
         
                singleDevice.WriteTag = null;
                singleDevice.stopInventory();
                singleDevice.disconnect();
                singleDevice.connect();
                Thread.Sleep(1000);
                singleDevice.startInventoryReal();
            }
           
  
            

        }
    }
}
