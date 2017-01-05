using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;

namespace BPCSDownload
{
    class BPCS
    {
        private string access_token = "";
        private const string filePath = "access_token";
        private const string host = "https://pcs.baidu.com/rest/2.0/";

        //委托(用于传递进度条信息到MainForm)
        public delegate void ProgressEventHander(long value, long maxnum);
        public ProgressEventHander progressEvent = null;
        //当前下载字节
        long downloadSize;
        long totalSize;
        //每次下载的字节数（和进度条相关）
        private const int segmentSize = 1 << 21;
        public uint Concurrency { get; set; }
        public BPCS(uint concurrency)
        {
            if (File.Exists(filePath))
                access_token = File.ReadAllText(filePath, Encoding.UTF8);
            System.Net.ServicePointManager.DefaultConnectionLimit = 500;
            Concurrency = concurrency;//并发数
        }
        private UInt64 GetFileSize(String path)
        {
            string uri = String.Format("https://pcs.baidu.com/rest/2.0/pcs/file?method=meta&access_token={0}&path={1}",access_token,path);
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (Stream readStream = response.GetResponseStream())
                {
                    StreamReader myStreamReader = new StreamReader(readStream, Encoding.UTF8);
                    string retString = myStreamReader.ReadToEnd();
                    myStreamReader.Close();
                    response.Close();
                    //使用Newtonsoft解析json
                    JObject jo = JObject.Parse(retString);
                    JArray ja = JArray.Parse(jo["list"].ToString());
                    UInt64 size = Convert.ToUInt64(ja[0]["size"].ToString());
                    response.Close();
                    return size;
                }
            }
            catch
            {
                return 0;
            }
        }
        /// <summary>
        /// 返回该路径下的所有文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<DownloadFile> GetFiles(String path)
        {
            List<DownloadFile> fileList = new List<DownloadFile>();
            string uri = string.Format("https://pcs.baidu.com/rest/2.0/pcs/file?method=list&access_token={0}&path={1}", access_token,path);
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (Stream readStream = response.GetResponseStream())
                {
                    StreamReader myStreamReader = new StreamReader(readStream, Encoding.UTF8);
                    string retString = myStreamReader.ReadToEnd();
                    myStreamReader.Close();
                    response.Close();
                    //使用Newtonsoft解析json
                    JObject jo = JObject.Parse(retString);
                    JArray ja = JArray.Parse(jo["list"].ToString());
                    if (ja.Count == 0)//该path为文件路径或空文件夹
                    {
                        UInt64 size = GetFileSize(path);
                        if(size!=0)
                            fileList.Add(new DownloadFile(path,0,size));
                    }
                    else//非空文件夹路径
                    {
                        foreach (JObject file in ja)
                        {
                            Int32 isdir = Convert.ToInt32(file["isdir"].ToString());
                            String filePath = file["path"].ToString();
                            UInt64 size = Convert.ToUInt64(file["size"].ToString());
                            fileList.Add(new DownloadFile(filePath, isdir,size));
                        }
                    }
                    response.Close();
                    return fileList;
                }
            }
            catch
            {
                return fileList;
            }
        }
        public bool Download(DownloadFile file)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string uri = host + "pcs/file?method=download&access_token=" + access_token+"&path="
                +System.Web.HttpUtility.UrlEncode(file.Path,Encoding.UTF8);
            string directory = GetDirectory(desktopPath + file.Path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string filePath = directory + "\\" + Path.GetFileName(file.Path);
            if(File.Exists(filePath))
            {
                if (DialogResult.No == MessageBox.Show("The file allready exists,do you want to override it?",
                    "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                    return true;
            }
            FileStream fileStream = new FileStream(filePath,FileMode.Create);
            downloadSize = 0;
            totalSize = (long)file.Size;
            try
            {
                int count =0;
                long offset = 0;
                //防止正在下载该文件时，Concurrency被更改
                //如果Concurrency变大，下面的循环会数组越界
                uint concurrency = Concurrency;
                Task<byte[]>[] tasks = new Task<byte[]>[concurrency];
                while(offset<totalSize)
                {
                    //晕啊，此处必须用tmp保存offset的值
                    //下面“offset += segmentSize;”对offset的更改会影响到已经传入线程
                    //中的offset！！
                    long tmp = offset;
                    tasks[count] = Task.Factory.StartNew<byte[]>(() =>
                    {
                        return SegmentDownload(uri, tmp, tmp + segmentSize - 1);    
                    });
                    offset += segmentSize;
                    ++count;
                    if (offset >= totalSize || count == concurrency)
                    {
                        //等待下载完
                        for(int i=0;i<count;++i)
                        {
                            if(tasks[i].Status==TaskStatus.Running)
                                tasks[i].Wait();
                            fileStream.Write(tasks[i].Result, 0, tasks[i].Result.Length);
                        }
                        count = 0;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                fileStream.Close();
            }
        }
        private byte[] SegmentDownload(String uri,long from,long to)
        {
            HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
            request.Method = "GET";
            if (to >= totalSize)
                to = totalSize-1;
            //if (from > to)
            //    return new byte[0];
            request.AddRange(from, to);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (Stream readStream = response.GetResponseStream())
            {
                int offset = 0;
                int count = (int)response.ContentLength;
                byte[] buffer = new byte[count];
                //一次读不完整，不知道为什么
                while (offset < count)
                {
                    offset += readStream.Read(buffer, offset, count - offset);
                    //progressEvent(downloadSize+offset, totalSize);
                }
                response.Close();
                //request.Abort();
                //request = null;
                downloadSize += count;
                progressEvent(downloadSize, totalSize);
                return buffer;
            }
        }

        private String GetDirectory(String path)
        {
            String directory = System.IO.Path.GetDirectoryName(path);
            int index = directory.IndexOf("\\apps\\UniDrive");
            directory = directory.Remove(index, 14);
            return directory;
        }
        /// <summary>
        /// 保存access_token到本地文件
        /// </summary>
        /// <param name="access_token"></param>
        public void Save(string access_token)
        {
            try
            {
                File.WriteAllText(filePath, access_token, Encoding.UTF8);
            }
            catch
            {
            }
        }
        /// <summary>
        /// 获取用户名
        /// </summary>
        /// <returns></returns>
        public string GetUserName()
        {
            string uri = string.Format("https://openapi.baidu.com/rest/2.0/passport/users/getInfo?access_token={0}", access_token);
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                //request.KeepAlive = false;
                request.Method = "GET";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (Stream readStream = response.GetResponseStream())
                {
                    StreamReader myStreamReader = new StreamReader(readStream, Encoding.UTF8);
                    string retString = myStreamReader.ReadToEnd();
                    myStreamReader.Close();
                    response.Close();
                    //使用了第三方库Newtonsoft解析json
                    JObject jo = JObject.Parse(retString);
                    return System.Web.HttpUtility.UrlDecode(jo["username"].ToString());
                }
            }
            catch
            {
                return "";
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool Login()
        {
            access_token = "";
            LoginForm login = new LoginForm();
            login.ShowDialog();
            if (login.IsLogin)
            {
                access_token = login.Access_Token;
                Save(access_token);
                return true;
            }
            return false;
        }
        /// <summary>
        /// 退出账户，删除access_token
        /// </summary>
        public void Logout()
        {
            access_token = "";
            File.Delete(filePath);
        }
        /// <summary>
        /// 判断access_token是否合法或已过期
        /// 目前只想到了“通过已经得到的access_token发送请求，看是否抛出异常”这种方式
        /// </summary>
        /// <returns></returns>
        public bool isValidate()
        {
            string uri = host + "/pcs/quota?method=info&access_token=" + access_token;
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";
                //request.KeepAlive = false;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                        return true;
                }
                request = null;
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
