using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using Reader;
using RFIDService.Models;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RFIDService.Tools
{
    internal class SingleDevice
    {

        [DllImport("winmm")]
        public static extern uint timeGetTime();

        [DllImport("winmm")]
        private static extern void timeBeginPeriod(int t);
        [DllImport("winmm")]
        static extern void timeEndPeriod(int t);

        /// <summary>
        /// 当前要写的标签
        /// </summary>
        volatile String writeTag;
       
        /// <summary>
        /// 最读写次数
        /// </summary>
        int maxWriteTimes=0;
        /// <summary>
        /// 是否连接
        /// </summary>
        bool isConnected=false;
        /// <summary>
        /// 是否开启循环读取数据
        /// </summary>
        public bool isLoop = false;
        /// <summary>
        /// 错误信息
        /// </summary>
        private  string strException = string.Empty;
        /// <summary>
        /// 串口号
        /// </summary>
        string strComPort;
        /// <summary>
        /// 波特率
        /// </summary>
        int nBaudrate = 115200;
        
        public ReaderMethod reader { get; }
        public bool IsConnected { get => isConnected; set => isConnected = value; }
        public string WriteTag { get => writeTag; set => writeTag = value; }
       
        public int MaxWriteTimes { get => maxWriteTimes; set => maxWriteTimes = value; }


        /// <summary>
        /// 单例模式
        /// </summary>
        private static SingleDevice singleDevice = new SingleDevice();
        private SingleDevice()
        {
            INIParser ini = new INIParser();
            ini.Open(@"config.ini");
            strComPort = ini.ReadValue("COM", "COM", "NULL");
            ini.Close();
            WriteTag = null;
            //strComPort = ConfigurationManager.AppSettings["COM"];
            reader = new ReaderMethod();
            reader.m_OnInventoryTag = onInventoryTag;
            reader.m_OnInventoryTagEnd = onInventoryTagEnd;
            reader.m_OnExeCMDStatus = onExeCMDStatus;
            //reader.m_RefreshSetting = refreshSetting;
            reader.m_OnOperationTag = onOperationTag;
            reader.m_OnOperationTagEnd = onOperationTagEnd;
            //reader.m_OnFastSwitchAntInventoryTagEnd = onFastSwitchAntInventoryTagEnd;
            //reader.m_OnGetInventoryBufferTagCount = onGetInventoryBufferTagCount;
            //reader.m_OnInventory6BTag = onInventory6BTag;
            //reader.m_OnInventory6BTagEnd = onInventory6BTagEnd;
            //reader.m_OnRead6BTag = onRead6BTag;
            //reader.m_OnWrite6BTag = onWrite6BTag;
            //reader.m_OnLock6BTag = onLock6BTag;
            //reader.m_OnLockQuery6BTag = onLockQuery6BTag;
            //reader.ReceiveCallback = onReceiveCallback;

        }
        public static SingleDevice getInstance()  { return singleDevice; }
        #region 设备的一系列操作函数
        /// <summary>
        /// 打开读写器
        /// </summary>
        public void connect()
        {
            //Processing serial port to connect reader.
           
            int nRet = reader.OpenCom(strComPort, nBaudrate, out strException);
            if (nRet != 0)
            {
                string strLog = "Connection failed, failure cause: " + strException;
                IsConnected=false;
                Console.WriteLine(strLog);
                MessageBox.Show("读写器未连接，请检查读写器连接和配置文件后重新打开应用！");
                System.Environment.Exit(0);
            }
            else
            {
                string strLog = "Connect" + strComPort + "@" + nBaudrate.ToString();
                Console.WriteLine("读写器已连接:"+strLog);
                //reader.Reset(0xFF);
              
                reader.SetBeeperMode((byte)0xFF, (byte)0x00);
                IsConnected=true;
              
            }
        }
        /// <summary>
        /// 关闭读写器
        /// </summary>
        public void disconnect() {
            if (IsConnected)
            {
                reader.CloseCom();
                IsConnected=false;
            }
            else {
                Console.WriteLine ("请检查读写器连接！");
            }
        }
        /// <summary>
        /// 开始盘存
        /// </summary>
        /// <returns></returns>
        public bool startInventoryReal() 
        {
            if (reader.InventoryReal((byte)0xFF, (byte)0xFF) != 0)
            {
                return false;
            }
            else {
                isLoop = true;
                return true;
            }
           
           
        }
        /// <summary>
        /// 停止盘存
        /// </summary>
        public void stopInventory() {
            isLoop = false;
        }
        /// <summary>
        /// 写操作
        /// </summary>
        /// <param name="toBeOperatedTag">写的编号</param>
        public void writeCurrentTag(string toBeOperatedTag, string deviceRecongnizeCode) {
            stopInventory();
            Thread.Sleep(300);
            string[] reslut = DataConvert.StringToStringArray(deviceRecongnizeCode, 2);
            byte[] btAryEpc = DataConvert.StringArrayToByteArray(reslut, reslut.Length);
            reader.SetAccessEpcMatch(0xFF, 0x00, Convert.ToByte(btAryEpc.Length), btAryEpc);
            string[] codes = DataConvert.StringToStringArray(toBeOperatedTag, 2);
            reader.WriteTag((byte)0xFF, DataConvert.StringToByteArray("00 00 00 00 00"), (byte)0x01, (byte)0x02, (byte)4, DataConvert.StringArrayToByteArray(codes,codes.Length));
            Thread.Sleep(300);
            startInventoryReal();
           

        }



        
        #endregion




        #region Reader中的委托

        void onReceiveCallback(byte[] btAryReceiveData)
        {
            string str = "";
            for (int i = 0; i < btAryReceiveData.Length; i++)
            {
                str += Convert.ToString(btAryReceiveData[i], 16) + "  ";
            }
            //注释掉没用的输出！
            Console.WriteLine("cmd data ： " + str);
        }

        void refreshSetting(ReaderSetting readerSetting)
        {
            Console.WriteLine("Version:" + readerSetting.btMajor + "." + readerSetting.btMinor);
        }

        void onExeCMDStatus(byte cmd, byte status)
        {
            if (isLoop && (cmd == CMD.REAL_TIME_INVENTORY))
            {
                reader.ResetInventoryBuffer(0xFF);
                reader.InventoryReal((byte)0xFF, (byte)0xFF);
            }
          
            Console.WriteLine("CMD execute CMD:" + CMD.format(cmd) + "++Status code:" + ERROR.format(status));
            Console.WriteLine("cmd:"+cmd);
            Console.WriteLine("status"+status);
            if (cmd==CMD.WRITE_TAG&&status==ERROR.TAG_WRITE_ERROR) {
                reader.InventoryReal((byte)0xFF, (byte)0xFF);
            }
              
           
        }

        void onInventoryTag(RXInventoryTag tag)
        {
            //Console.WriteLine("调用盘点！");
            reader.ResetInventoryBuffer(0xFF);
            WriteTag = tag.strEPC;

            Console.WriteLine("Inventory EPC:" + tag.strEPC);
            Console.WriteLine("Inventory Ant:" + tag.btAntId);

        }

        void onInventoryTagEnd(RXInventoryTagEnd tagEnd)
        {
          
            if (isLoop)
            {
                reader.InventoryReal((byte)0xFF, (byte)0xFF);
            }
            else {
                Console.WriteLine("关闭盘点！");
            }
        }

        void onFastSwitchAntInventoryTagEnd(RXFastSwitchAntInventoryTagEnd tagEnd)
        {
            Console.WriteLine("Fast Inventory end:" + tagEnd.mTotalRead);
        }

        void onInventory6BTag(byte nAntID, String strUID)
        {
            Console.WriteLine("Inventory 6B Tag:" + strUID);
        }

        void onInventory6BTagEnd(int nTagCount)
        {
            Console.WriteLine("Inventory 6B Tag:" + nTagCount);
        }

        void onRead6BTag(byte antID, String strData)
        {
            Console.WriteLine("Read 6B Tag:" + strData);
        }

        void onWrite6BTag(byte nAntID, byte nWriteLen)
        {
            Console.WriteLine("Write 6B Tag:" + nWriteLen);
        }

        void onLock6BTag(byte nAntID, byte nStatus)
        {
            Console.WriteLine("Lock 6B Tag:" + nStatus);
        }

        void onLockQuery6BTag(byte nAntID, byte nStatus)
        {
            Console.WriteLine("Lock query 6B Tag:" + nStatus);
        }

        void onGetInventoryBufferTagCount(int nTagCount)
        {
            Console.WriteLine("Get Inventory Buffer Tag Count" + nTagCount);
        }

        void onOperationTag(RXOperationTag tag)
        {
            Console.WriteLine("Operation Tag" + tag.strData);
        }

        void onOperationTagEnd(int operationTagCount)
        {
            Console.WriteLine("Operation Tag End" + operationTagCount);
            if (operationTagCount==1) {
                //开启下一标签的写入
                 Console.WriteLine("写入成功！");
                 WriteTag = null;
                 stopInventory();
                //disconnect();
                //connect();
                 startInventoryReal();
            }

        }

        #endregion

    }
}
