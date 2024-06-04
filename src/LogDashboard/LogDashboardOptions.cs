using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using DapperExtensions.Sql;
using LogDashboard.Authorization;
using LogDashboard.Models;
using Microsoft.AspNetCore.Authorization;

namespace LogDashboard
{
    public class LogDashboardOptions
    {
        /// <summary>
        /// Default value : Log Dashboard
        /// </summary>
        public string Brand { get; set; }

        /// <summary>
        /// Url match
        /// </summary>
        public string PathMatch { get; set; }

        public bool FileSource { get; set; }

        /// <summary>
        /// Log files path
        /// </summary>
        public string RootPath { get; set; }

        public bool DatabaseSource { get; set; }

        internal Func<DbConnection> DbConnectionFactory { get; set; }

        internal ISqlDialect SqlDialect { get; set; }

        internal Type LogModelType { get; set; }

        public TimeSpan CacheExpires { get; set; }

        internal List<IAuthorizeData> AuthorizeData { get; set; } = new List<IAuthorizeData>();

        internal List<ILogDashboardAuthorizationFilter> AuthorizationFiles { get; set; }

        internal List<PropertyInfo> CustomPropertyInfos { get; set; }
        internal string LogSchemaName { get; set; }

        internal string LogTableName { get; set; }

        /// <summary>
        /// file log field Delimiter
        /// </summary>
        public string FileFieldDelimiter { get; set; }
        
        /// <summary>
        /// file log end Delimiter
        /// </summary>
        public string FileEndDelimiter { get; set; }

        /// <summary>
        /// 使用正则表达式作为分隔符，默认false。如果设置为true，则FileFieldDelimiter将被视为正则表达式。
        /// 支持格式（顺序和文本要一致，Logger是可选的，其他是必须的）：记录时间：(.*?)\n线程ID:(.*?)\n日志级别：(.*?)\n(?:Logger:(.*?)\n)?跟踪描述：(.*?)\n堆栈信息：(.*)
        /// </summary>
        public bool FileFieldDelimiterWithRegex { get; set; }


        public void AddAuthorizeAttribute(params IAuthorizeData[] authorizeAttributes)
        {
            if (authorizeAttributes != null)
            {
                AuthorizeData.AddRange(authorizeAttributes);
            }
        }


        public void AddAuthorizationFilter(params ILogDashboardAuthorizationFilter[] filters)
        {
            if (filters != null)
            {
                AuthorizationFiles.AddRange(filters);
            }
        }

        public void CustomLogModel<T>() where T : class, ILogModel
        {
            LogModelType = typeof(T);

            CustomPropertyInfos = LogModelType.GetProperties().Where(x => !x.Name.Equals("LongDate", StringComparison.CurrentCultureIgnoreCase) &&
                                              !x.Name.Equals("Id", StringComparison.CurrentCultureIgnoreCase) &&
                                              !x.Name.Equals("Level", StringComparison.CurrentCultureIgnoreCase) &&
                                              !x.Name.Equals("Logger", StringComparison.CurrentCultureIgnoreCase) &&
                                              !x.Name.Equals("Message", StringComparison.CurrentCultureIgnoreCase) &&
                                              !x.Name.Equals("Exception", StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        public LogDashboardOptions()
        {
            Brand = "Log Dashboard";
            CustomPropertyInfos = new List<PropertyInfo>();
            FileSource = true;
            FileFieldDelimiter = "||";
            FileEndDelimiter = "||end";
            FileFieldDelimiterWithRegex = false;
            PathMatch = "/LogDashboard";
            LogModelType = typeof(LogModel);
            AuthorizationFiles = new List<ILogDashboardAuthorizationFilter>();
            CacheExpires = TimeSpan.FromMinutes(5);
        }

        public void UseDataBase(Func<DbConnection> dbConnectionFactory, string schemaName = "dbo", string tableName = "log", ISqlDialect sqlDialect = null)
        {
            LogSchemaName = schemaName;
            LogTableName = tableName;
            DatabaseSource = true;
            FileSource = false;
            DbConnectionFactory = dbConnectionFactory;
            SqlDialect = sqlDialect ?? new SqlServerDialect();
        }
    }
}

