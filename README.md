# .NET Wavefront SDK Tanzu Application Service Integration

## Overview

In this repository we setup two projects, a Weather UI and a Weather API.  The WeatherUI will make requests to the WeatherAPI and both services will be instrumented using the [Wavefront ASP.NET Core SDK](https://github.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp) to allow the applications to communicate with Wavefront.

1. Create a new workspace directory and navigate to that directory

   ```bash
   mkdir <workspace name>
   cd <workspace name>
   ```

2. Create two projects one named backend and one named frontend.  The backend project will be a Web API based service while the frontend project will be a Blazor server application.

   ```cs
    dotnet new webapi --name backend
   ```

    ```cs
    dotnet new blazorserver -o frontend --no-https --help
   ```

3. In each project add the [Wavefront.AspNetCore.SDK.CSharp](https://www.nuget.org/packages/Wavefront.AspNetCore.SDK.CSharp/) nuget package:

   ```bash
   dotnet add .\frontend\frontend.csproj package Wavefront.AspNetCore.SDK.CSharp
   ```

   ```bash
   dotnet add .\backend\backend.csproj package Wavefront.AspNetCore.SDK.CSharp
   ```

   Create a file named `WavefrontExtensions.cs` with the following contents.  Copy the file to root of *both* projects.  This file includes SDK code that allows us to connect to a Wavefront Proxy or to Wavefront with Direct Ingestion.

   ```cs
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.DependencyInjection;

   using Wavefront.AspNetCore.SDK.CSharp.Common;
   using Wavefront.OpenTracing.SDK.CSharp;
   using Wavefront.OpenTracing.SDK.CSharp.Reporting;
   using Wavefront.SDK.CSharp.Common.Application;
   using Wavefront.SDK.CSharp.Proxy;

   using OpenTracing;
   using Wavefront.SDK.CSharp.DirectIngestion;

   namespace wavefront_sdk
   {
      public static class SteeltoeWavefrontProxyExtensions
      {
         public abstract class WavefrontCredentials
         {
               public WavefrontCredentials()
               {
                  ReportingIntervalSeconds = 30;
                  FlushIntervalSeconds = 2;
               }
               
               public string Application { get; set; }
               public string Service { get; set; }
               public string Cluster { get; set; }
               public string Shard { get; set; }
               public string Source {get; set;}
               public int ReportingIntervalSeconds {get; set;}
               public int FlushIntervalSeconds { get; set; }
         }
         public class WavefrontProxyOptions : WavefrontCredentials
         {
               public WavefrontProxyOptions()
               {
                  Port = 2878;
                  DistributionPort = 2878;
                  TracingPort = 30000;
               }

               public const string WavefrontProxy = "wavefront-proxy";
               public string Hostname { get; set; }
               public int Port { get; set; }
               public int DistributionPort { get; set; }
               public int TracingPort { get; set; }
         }

         public class WavefrontDirectIngestionOptions : WavefrontCredentials
         {
               public const string WavefrontDirectIngestion = "wavefront-direct-ingestion";

               public WavefrontDirectIngestionOptions()
               {
                  MaxQueueSize = 100_000;
                  BatchSize = 20_000;
               }

               public string Hostname { get; set; }
               public int MaxQueueSize { get; set; }
               public int BatchSize { get; set; }
               public string Token { get; set; }
         }

         public static IServiceCollection AddSteeltoeWavefrontProxy(this IServiceCollection services, IConfiguration configuration)
         {
               var waveFrontProxyConfiguration = 
                  configuration.GetSection(WavefrontProxyOptions.WavefrontProxy).Get<WavefrontProxyOptions>();

               var wfProxyClientBuilder = new WavefrontProxyClient.Builder(waveFrontProxyConfiguration.Hostname);
               wfProxyClientBuilder.MetricsPort(waveFrontProxyConfiguration.Port);
               wfProxyClientBuilder.DistributionPort(waveFrontProxyConfiguration.DistributionPort);
               wfProxyClientBuilder.TracingPort(waveFrontProxyConfiguration.TracingPort);
               wfProxyClientBuilder.FlushIntervalSeconds(waveFrontProxyConfiguration.TracingPort);
               var wavefrontSender = wfProxyClientBuilder.Build();

               var applicationTags = new ApplicationTags.Builder(waveFrontProxyConfiguration.Application, waveFrontProxyConfiguration.Service)
               .Cluster(waveFrontProxyConfiguration.Cluster)
               .Shard(waveFrontProxyConfiguration.Shard)
               .Build();

               var wfAspNetCoreReporter = new WavefrontAspNetCoreReporter.Builder(applicationTags)
                  .WithSource(waveFrontProxyConfiguration.Source)
                  .ReportingIntervalSeconds(waveFrontProxyConfiguration.ReportingIntervalSeconds)
                  .Build(wavefrontSender);

               System.Console.WriteLine(wfAspNetCoreReporter);

               var wavefrontSpanReporter = new WavefrontSpanReporter.Builder()
               .Build(wavefrontSender);

               ITracer tracer = new WavefrontTracer.Builder(wavefrontSpanReporter, applicationTags).Build();

               services.AddWavefrontForMvc(wfAspNetCoreReporter, tracer);

               return services;
         }

         public static IServiceCollection AddSteeltoeWavefrontDirectIngestion(this IServiceCollection services, IConfiguration configuration)
         {
               var waveFrontDirectIngestionConfiguration = 
                  configuration.GetSection(WavefrontDirectIngestionOptions.WavefrontDirectIngestion)
                  .Get<WavefrontDirectIngestionOptions>();

               var applicationTags = 
                  new ApplicationTags.Builder(waveFrontDirectIngestionConfiguration.Application, waveFrontDirectIngestionConfiguration.Service)
                  .Cluster(waveFrontDirectIngestionConfiguration.Cluster)
                  .Shard(waveFrontDirectIngestionConfiguration.Shard)
                  .Build();

               var wfDirectIngestionClientBuilder = new WavefrontDirectIngestionClient.Builder(waveFrontDirectIngestionConfiguration.Hostname, waveFrontDirectIngestionConfiguration.Token);
               wfDirectIngestionClientBuilder.MaxQueueSize(waveFrontDirectIngestionConfiguration.MaxQueueSize);
               wfDirectIngestionClientBuilder.BatchSize(waveFrontDirectIngestionConfiguration.BatchSize);
               wfDirectIngestionClientBuilder.FlushIntervalSeconds(waveFrontDirectIngestionConfiguration.FlushIntervalSeconds);
               var wavefrontSender = wfDirectIngestionClientBuilder.Build();

               var wfAspNetCoreReporter = new WavefrontAspNetCoreReporter.Builder(applicationTags)
                  .WithSource(waveFrontDirectIngestionConfiguration.Source)
                  .ReportingIntervalSeconds(waveFrontDirectIngestionConfiguration.ReportingIntervalSeconds)
                  .Build(wavefrontSender);

               var wavefrontSpanReporter = new WavefrontSpanReporter.Builder()
               .Build(wavefrontSender);

               ITracer tracer = new WavefrontTracer.Builder(wavefrontSpanReporter, applicationTags).Build();

               services.AddWavefrontForMvc(wfAspNetCoreReporter, tracer);

               return services;
         }
      }
   }
   ```

4. In the frontend application make the following edits to communicate to the backend service:

   1. Replace the `WeatherForecastService` class with the following implementation:

      ```cs
      using System;
      using System.Linq;
      using System.Net.Http;
      using System.Net.Http.Json;
      using System.Threading.Tasks;

      namespace frontend.Data
      {
         public interface IWeatherForecastService
         {
            Task<WeatherForecast[]> GetForecastAsync(DateTime startDate);
         }

         public class WeatherForecastService : IWeatherForecastService
         {
            HttpClient _client;

            public WeatherForecastService(HttpClient client)
            {
                  _client = client;
            }

            public async Task<WeatherForecast[]> GetForecastAsync(DateTime startDate)
            {
                  var responseMessage = await _client.GetAsync("/weatherforecast");
                  return await responseMessage.Content.ReadFromJsonAsync<WeatherForecast[]>();
            }
         }
      }

      ```

   2. Go to `Startup.cs` and add the following using statement:

      ```cs
      using wavefront_sdk
      ```

   3. While still in `Startup.cs` update the `ConfigureServices` method with the following implementation:

      ```cs
      public void ConfigureServices(IServiceCollection services)
         {
               services.AddRazorPages();
               services.AddServerSideBlazor();
               services.AddHttpClient<IWeatherForecastService, WeatherForecastService>(c => c.BaseAddress = new Uri(Configuration.GetConnectionString("backend")));
               services.AddWavefrontProxy(Configuration);
         }
      ```

5. Go to the backend project and navigate to `Startup.cs`

   1. Add the following using statement:

      ```cs
      using wavefront_sdk
      ```

   2. In the last line of the `ConfigureServices` Method add the following line:

      ```cs
      services.AddWavefrontDirectIngestion(Configuration);
      ```

6. Add configuration for the respective project:

   1. frontend

      ```json
      "wavefront-proxy": {
         "Hostname": "IP/HOST Name of Your WavefrontP Proxy",
         "Application": "Weather Forecast", 
         "Service": "Weather UI"
      }
      ```

   2. backend

      ```json
      "wavefront-direct-ingestion": {
         "Hostname": "https://demo.wavefront.com",
         "Application": "Weather Forecast", 
         "Service": "Weather API"
      }
      ```

7. Build both applications:

   ```bash
   dotnet publish .\frontend\frontend.csproj -o .\frontend\publish
   ```

   ```bash
   dotnet publish .\backend\backend.csproj -o .\backend\publish
   ```

8. Create a Yaml file called manifest.yml with the following contents, substituting the relevant values in the placeholders below.  Ensure the WeatherUIs **ConnectionStrings:backend** value matches the value of the WeatherAPIs value for its route.

   ```yml
   applications:
   - name: WeatherUI
   path: .\frontend\publish
   buildpacks:
   - https://github.com/cloudfoundry/dotnet-core-buildpack
   services:
   - wavefront-proxy
   env:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings:backend: [URL to backend app:WeatherAPI.apps.internal]
   - name: WeatherAPI
   path: .\backend\publish
   routes:
   - route: [Backend URL:WeatherAPI.apps.internal]
   buildpacks:
   - https://github.com/cloudfoundry/dotnet-core-buildpack
   env:
      ASPNETCORE_ENVIRONMENT: Development
      wavefront-direct-ingestion: [TOKEN_FROM_WAVEFRONT:99999999-9999-9999-9999-99999999999]
   ```

9. Push our application

   ```bash
   cf push
   ```
