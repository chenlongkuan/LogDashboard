using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogDashboard.Cache;
using LogDashboard.Extensions;
using LogDashboard.Models;

namespace LogDashboard.Repository.File
{
    public class FileUnitOfWork<T> : IUnitOfWork where T : class, ILogModel, new()
    {
        private List<T> _logs;

        private readonly LogDashboardOptions _options;

        private readonly ILogDashboardCacheManager<T> _cacheManager;

        protected static readonly List<LogFile> LogFiles = new List<LogFile>();

        public FileUnitOfWork(
            LogDashboardOptions options,
            ILogDashboardCacheManager<T> cacheManager)
        {
            _options = options;
            _cacheManager = cacheManager;
            _logs = new List<T>();
        }

        public List<T> GetLogs()
        {
            return _logs;
        }

        public async Task Open()
        {
            _logs = await _cacheManager.GetCache(LogDashboardConsts.LogDashboardLogsCache);

            if (_logs.Count > 0)
            {
                await ReadIncrementalLogs();
            }
            else
            {
                await ReadAllLogs();
            }
        }

        public void Close()
        {
            _logs = null;
        }

        private async Task ReadIncrementalLogs()
        {
            BuildLogFiles();
            var id = _logs.Max(x => x.Id);
            await ReadLogs(++id);
        }

        private void BuildLogFiles()
        {
            var rootPath = _options.RootPath ?? AppContext.BaseDirectory;

            if (!Directory.Exists(rootPath))
            {
                _logs.Add(CreateWarnItem(_logs.Last().Id + 1, $"{LogDashboardConsts.Root} Warn:日志文件目录不存在,请检查 LogDashboardOption.RootPath 配置!"));
            }

            var paths = Directory.GetFiles(rootPath, "*.log", SearchOption.AllDirectories);

            var logFiles = paths.Select(x => new LogFile
            {
                Path = x,
                LastModifyTime = System.IO.File.GetLastWriteTime(x),
                LastReadLine = 0
            }).ToList();

            if (!LogFiles.Any())
            {
                LogFiles.AddRange(logFiles);
            }
            else
            {
                foreach (var logFile in logFiles)
                {
                    var temp = LogFiles.FirstOrDefault(x => x.Path == logFile.Path);
                    if (temp == null)
                    {
                        LogFiles.AddRange(logFiles);
                        continue;
                    }

                    if (temp.LastModifyTime != logFile.LastModifyTime)
                    {
                        temp.ShouldRead = true;
                    }
                }
            }
        }


        private async Task ReadLogs(int id = 1)
        {

            foreach (var logFile in LogFiles.Where(x => x.ShouldRead))
            {
                var stringBuilder = new StringBuilder();

                using (var fileStream = new FileStream(logFile.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var streamReader = new StreamReader(fileStream, Encoding.Default))
                {
                    //Skip line
                    for (var i = 0; i < logFile.LastReadLine; i++)
                    {
                        await streamReader.ReadLineAsync();
                    }

                    while (!streamReader.EndOfStream)
                    {
                        stringBuilder.AppendLine(await streamReader.ReadLineAsync());
                        logFile.LastReadLine++;
                    }
                }

                var text = stringBuilder.ToString().Trim();
                if (_options.FileFieldDelimiterWithRegex)
                {
                    // 正则表达式匹配模式，匹配从"记录时间："开始到文件末尾的所有内容
                    var pattern = @"记录时间：[\s\S]*?(?=(记录时间：|$))";

                    // 使用RegexOptions.Singleline使得.匹配包括换行符在内的任意字符
                    var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);


                    foreach (Match match in matches)
                    {

                        // 提取每个日志条目的信息
                        string logEntry = match.Value.Trim();
                        // 正则表达式匹配单个日志条目的各个部分
                        string entryPattern = @"记录时间：(.*?)\n线程ID:(.*?)\n日志级别：(.*?)\n(?:Logger:(.*?)\n)?跟踪描述：(.*?)(?:\s*堆栈信息：(.*))?$";
                        Match entryMatch = Regex.Match(logEntry, entryPattern, RegexOptions.Singleline);
                        if (!entryMatch.Success) continue;
                        string recordTime = entryMatch.Groups[1].Value.Trim();
                        string threadId = entryMatch.Groups[2].Value.Trim();
                        string logLevel = entryMatch.Groups[3].Value.Trim();
                        string logger = entryMatch.Groups[4].Value.Trim();
                        string traceDescription = entryMatch.Groups[5].Value.Trim();
                        string stackTrace = entryMatch.Groups[6].Value.Trim();

                        var item = new T
                        {
                            Id = id,
                            LongDate = DateTime.ParseExact(recordTime, "yyyy-MM-dd HH:mm:ss fff", CultureInfo.InvariantCulture),
                            Level = logLevel,
                            Logger = logger,
                            Message = traceDescription,
                            ThreadId = threadId,
                            Exception = stackTrace
                        };
                        _logs.Add(item);
                        id++;

                    }
                }
                else
                {
                    var logLines = text.Replace("|| end", _options.FileEndDelimiter)
                                       .Split(new[] { _options.FileEndDelimiter }, StringSplitOptions.None)
                                       .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                    foreach (var logLine in logLines)
                    {
                        var line = logLine.Split(new[] { _options.FileFieldDelimiter }, StringSplitOptions.None);
                        if (line.Length > 1)
                        {
                            var item = new T
                            {
                                Id = id,
                                LongDate = DateTime.Parse(line.TryGetValue(0)),
                                Level = line.TryGetValue(1),
                                Logger = line.TryGetValue(2),
                                Message = line.TryGetValue(3),
                                Exception = line.TryGetValue(4)
                            };

                            var lineEnd = Math.Min(_options.CustomPropertyInfos.Count, line.Length - 5);
                            if (line.Length - 5 != _options.CustomPropertyInfos.Count && logLine == logLines.Last())
                            {
                                _logs.Add(CreateWarnItem(id, $"Warn: {Path.GetFileName(logFile.Path)} 文件内容与自定义日志模型不完全匹配,请检查代码!"));
                            }

                            for (var i = 0; i < lineEnd; i++)
                            {
                                _options.CustomPropertyInfos[i].SetValue(item, line.TryGetValue(i + 5));
                            }

                            _logs.Add(item);
                            id++;
                        }
                    }
                }



                logFile.ShouldRead = false;
            }

            await _cacheManager.SetCache(LogDashboardConsts.LogDashboardLogsCache, _logs);
        }


        private async Task ReadAllLogs()
        {
            LogFiles.Clear();
            BuildLogFiles();
            await ReadLogs();
        }

        private T CreateWarnItem(int id, string message)
        {
            return new T
            {
                Id = id,
                Logger = LogDashboardConsts.Root,
                LongDate = DateTime.Now,
                Level = LogLevelConst.Warn,
                Message = message
            };
        }

        public void Dispose()
        {
            Close();
        }
    }

    public class LogFile
    {
        public string Path { get; set; }

        public int LastReadLine { get; set; }

        public DateTime LastModifyTime { get; set; }

        public bool ShouldRead { get; set; } = true;
    }
}
