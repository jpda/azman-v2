using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twilio.Types;

namespace azman_v2
{
    public interface INotifier
    {
        Task Notify(NotificationMessage message);
        Task Notify(NotificationMessage message, NotificationChannel channel);
    }

    public class NotificationMessage
    {
        public string Message { get; set; }
        public DateTime NotificationTime { get; set; }
    }

    public class NotificationChannel
    {
        public string ChannelId { get; set; }
    }
}