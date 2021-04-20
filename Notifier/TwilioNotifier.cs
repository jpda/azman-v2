using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace azman_v2
{
    public class TwilioNotifier : INotifier
    {
        private readonly ILogger<TwilioNotifier> _logger;
        private readonly TwilioOptions _opts;
        public TwilioNotifier(IOptions<TwilioOptions> options, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TwilioNotifier>();
            _opts = options.Value;
            TwilioClient.Init(_opts.AccountSid, _opts.AccountToken);
        }

        public async Task Notify(NotificationMessage message)
        {
            try
            {
                foreach (var number in _opts.ToNumberList)
                {
                    var theMessage = await MessageResource.CreateAsync(
                    to: new PhoneNumber(number),
                    from: new PhoneNumber(_opts.FromNumber),
                    body: message.Message
                );
                }

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

    public class TwilioOptions
    {
        public string AccountSid { get; set; }
        public string AccountToken { get; set; }
        public string FromNumber { get; set; }
        public List<string> ToNumberList { get; set; }
    }
}