﻿using System;

namespace LogDashboard.Models
{
    public interface ILogModel
    {
        int Id { get; set; }

        DateTime LongDate { get; set; }

        string Level { get; set; }

        string Logger { get; set; }

        string Message { get; set; }

        string Exception { get; set; }

        string ThreadId { get; set; }
    }
}
