using System;
using System.IO;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using azman_v2.Auth;
using System.Reflection;
using Twilio;

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
            if (builder.GetContext().EnvironmentName == "Development")
            {
                builder.Services.AddSingleton<ITokenProvider, AzureCliTokenProvider>();
            }
            else
            {
                builder.Services.AddSingleton<ITokenProvider, AzureManagedIdentityServiceTokenProvider>();
            }
            builder.Services.AddSingleton<IResourceManagementService, AzureResourceManagementService>();
            builder.Services.AddSingleton<IScanner, Scanner>();
            builder.Services.AddSingleton<INotifier, TwilioNotifier>();
        }
    }
}