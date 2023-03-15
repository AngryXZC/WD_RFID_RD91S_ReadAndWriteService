using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFIDService.Models
{
    public class OperateModel
    {
       /// <summary>
       /// 待操作的标签
       /// 其中
       /// 待写入的标签号
       /// </summary>
        public string toBeWriteTag { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string targetTag { get; set; }


        public string currentTag { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool resultFlag { get; set; }
        
       
    }
}
