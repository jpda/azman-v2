using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using azman_v2.Auth;

using Azure.Identity;
using Azure.Core;

[assembly: FunctionsStartup(typeof(azman_v2.Startup))]

namespace azman_v2
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            FunctionsHostBuilderContext context = builder.GetContext();

            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
                //.AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AZMAN-AAC-CONNECTION"), optional: true)
                //.AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<TokenCredential, ManagedIdentityCredential>();
            builder.Services.AddSingleton<ITokenProvider, AzureIdentityTokenProvider>();
            builder.Services.AddSingleton<IResourceManagementService, AzureResourceManagementService>();
            builder.Services.AddOptions<ScannerOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                config.GetSection("Scanner").Bind(opts);
            });
            builder.Services.AddSingleton<IScanner, Scanner>();
            builder.Services.AddOptions<TwilioOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                var twilioConfig = config.GetSection("Twilio");
                opts.AccountSid = twilioConfig["AccountSid"];
                opts.AccountToken = twilioConfig["AccountToken"];
                opts.FromNumber = twilioConfig["FromNumber"];
                opts.ToNumberList = twilioConfig["ToNumberList"].Split(';').ToList();
            });
            builder.Services.AddSingleton<INotifier, TwilioNotifier>();
        }
    }
}