using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFIDService.Models
{
    public class Root<K>
    {
        /// <summary>
        /// 
        /// </summary>
        public bool success { get; set; }
        /// <summary>
        /// 操作成功！
        /// </summary>
        public string message { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public K result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long timestamp { get; set; }
    }
}
