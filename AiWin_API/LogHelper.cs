﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AiWin_API
{
    /// <summary>
    ///     日志类型
    /// </summary>
    public enum LogType
    {
        /// <summary>
        ///     插入型
        /// </summary>
        Insert,

        /// <summary>
        ///     更新型
        /// </summary>
        Update,

        /// <summary>
        ///     所有
        /// </summary>
        All,

        /// <summary>
        ///     结尾，放在最后
        /// </summary>
        End
    }

    /// <summary>
    ///     记录日志
    /// </summary>
    public class LogHelper
    {
        #region 构造函数

        /// <summary>
        ///     静态构造函数
        /// </summary>
        static LogHelper()
        {
            qMsg = new Queue<string>();
            SetCreatedFileCount();
            RunThread();
        }

        #endregion

        #region 自定义变量

        /// <summary>
        ///     异常信息的队列
        /// </summary>
        private static readonly Queue<string> qMsg;

        /// <summary>
        ///     文件大小最大值，单位：Mb
        /// </summary>
        private static int maxFileSize = 10;

        /// <summary>
        ///     当天创建同一类型的日志文件的个数
        /// </summary>
        private static readonly int[] createdFileCounts = new int[(int) LogType.End];

        /// <summary>
        ///     日志文件存放路径
        /// </summary>
        private static string logFilePath = "";

        #endregion

        #region 属性

        /// <summary>
        ///     文件大小最大值，单位：Mb。小于0时则不限制
        /// </summary>
        public static int MaxFileSize
        {
            get { return maxFileSize; }
            set { maxFileSize = value; }
        }

        /// <summary>
        ///     日志文件存放路径
        /// </summary>
        public static string LogFilePath
        {
            set { logFilePath = value; }
            get
            {
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    return logFilePath;
                }
                return AppDomain.CurrentDomain.BaseDirectory + "\\Log\\" + DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        #endregion

        #region 辅助

        /// <summary>
        ///     获取日志文件的全路径
        /// </summary>
        /// <param name="logType"></param>
        /// <returns></returns>
        private static string GetLogPath(LogType logType, bool isCreateNew)
        {
            var logPath = LogFilePath;
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
                //看成是新的一天，要将昨天的数据清空
                for (var i = 0; i < createdFileCounts.Length; i++)
                {
                    createdFileCounts[i] = 0;
                }
            }
            switch (logType)
            {
                case LogType.Insert:
                    logPath = logPath + "\\" + "Insert";
                    break;
                case LogType.Update:
                    logPath = logPath + "\\" + "Update";
                    break;
                default:
                    logPath = logPath + "\\" + "All";
                    break;
            }
            if (isCreateNew)
            {
                var num = ++createdFileCounts[(int) logType];
                logPath += $"({num}).log";
                return logPath;
            }

            logPath += ".log";
            //createdFileCounts[(int)logType] = 0;
            if (!File.Exists(logPath))
            {
                //File.Create(logPath);
                var fs = File.Create(logPath);
                fs.Close();
                fs.Dispose();
            }

            return logPath;
        }

        /// <summary>
        ///     运行线程
        /// </summary>
        private static void RunThread()
        {
            ThreadPool.QueueUserWorkItem(u =>
            {
                while (true)
                {
                    var tmsg = string.Empty;
                    lock (qMsg)
                    {
                        if (qMsg.Count > 0)
                            tmsg = qMsg.Dequeue();
                    }

                    //往日志文件中写错误信息                     
                    if (!string.IsNullOrEmpty(tmsg))
                    {
                        var index = tmsg.IndexOf("&&", StringComparison.Ordinal);
                        var logTypeStr = tmsg.Substring(0, index);
                        var logType = LogType.All;
                        if (logTypeStr == $"{LogType.Insert}")
                        {
                            logType = LogType.Insert;
                        }
                        else if (logTypeStr == $"{LogType.Update}")
                        {
                            logType = LogType.Update;
                        }

                        //记录所有日志
                        WriteLog(tmsg.Substring(index + 2));
                        //分开记录日志
                        if (logType != LogType.All)
                        {
                            WriteLog(tmsg.Substring(index + 2), logType);
                        }
                    }

                    if (qMsg.Count <= 0)
                    {
                        Thread.Sleep(1000);
                    }
                }
            });
        }

        /// <summary>
        ///     程序刚启动时 检测已创建的日志文件个数
        /// </summary>
        private static void SetCreatedFileCount()
        {
            var logPath = LogFilePath;
            if (!Directory.Exists(logPath))
            {
                for (var i = 0; i < createdFileCounts.Length; i++)
                {
                    createdFileCounts[i] = 0;
                }
            }
            else
            {
                var dirInfo = new DirectoryInfo(logPath);
                var fileInfoes = dirInfo.GetFiles("*.log");
                foreach (var fi in fileInfoes)
                {
                    var fileName = Path.GetFileNameWithoutExtension(fi.FullName).ToLower();
                    if (fileName.Contains('(') && fileName.Contains(')'))
                    {
                        fileName = fileName.Substring(0, fileName.LastIndexOf('('));
                        switch (fileName)
                        {
                            case "insert":
                                createdFileCounts[(int) LogType.Insert]++;
                                break;
                            case "update":
                                createdFileCounts[(int) LogType.Update]++;
                                break;
                            case "all":
                                createdFileCounts[(int) LogType.All]++;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        #endregion

        #region 写日志

        /// <summary>
        ///     写日志
        /// </summary>
        /// <param name="strLog">日志内容</param>
        public static void WriteLog(string strLog)
        {
            WriteLog(strLog, LogType.All);
        }

        /// <summary>
        ///     写日志
        /// </summary>
        /// <param name="strLog">日志内容</param>
        /// <param name="logType">日志类型</param>
        public static void WriteLog(string strLog, LogType logType)
        {
            if (string.IsNullOrEmpty(strLog))
            {
                return;
            }
            strLog = strLog.Replace("\n", "\r\n");

            FileStream fs = null;
            try
            {
                var logPath = GetLogPath(logType, false);
                var fileInfo = new FileInfo(logPath);
                if (MaxFileSize > 0 && fileInfo.Length > 1024*1024*MaxFileSize)
                {
                    fileInfo.MoveTo(GetLogPath(logType, true));
                }
                fs = File.Open(logPath, FileMode.OpenOrCreate);
                //fs = File.OpenWrite(logPath);
                var btFile = Encoding.UTF8.GetBytes(strLog);
                //设定书写的開始位置为文件的末尾  
                fs.Position = fs.Length;
                //将待写入内容追加到文件末尾  
                fs.Write(btFile, 0, btFile.Length);
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }
        }

        /// <summary>
        ///     写入错误日志队列
        /// </summary>
        public static void WriteLogAsync(string strLog, LogType logType)
        {
            //将错误信息添加到队列中
            lock (qMsg)
            {
                qMsg.Enqueue($"{logType}&&{strLog}\r\n");
            }
        }

        #endregion
    }
}