using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public interface INotifier
{
    Task Notify(NotificationMessage message);
    Task Notify(NotificationMessage message, NotificationChannel channel);
}

public class TwilioNotifier : INotifier
{
    private readonly ILogger<TwilioNotifier> _logger;
    public TwilioNotifier(IConfiguration config, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TwilioNotifier>();
        var twilioAccountSid = config["TwilioAccountSid"];
        var twilioAccountAuthToken = config["TwilioAccountToken"];
        TwilioClient.Init(twilioAccountSid, twilioAccountAuthToken);
    }

    public async Task Notify(NotificationMessage message)
    {
        try
        {
            var theMessage = await MessageResource.CreateAsync(
                to: new PhoneNumber("+19802072561"),
                from: new PhoneNumber("+18146629626"),
                body: message.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    public Task Notify(NotificationMessage message, NotificationChannel channel)
    {
        throw new NotImplementedException();
    }
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