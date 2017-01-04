using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPCSDownload
{
    class DownloadFile
    {
        public String Path { get; set; }
        public Int32 Isdir { get; set; }
        public UInt64 Size { get; set; }
        public DownloadFile()
        {

        }
        public DownloadFile(String path,int isdir,UInt64 size)
        {
            Path = path;
            Isdir = isdir;
            Size = size;
        }
    }
}
