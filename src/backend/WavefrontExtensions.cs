using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wavefront.AspNetCore.SDK.CSharp.Common;
using Wavefront.OpenTracing.SDK.CSharp;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.SDK.CSharp.Common.Application;
using Wavefront.SDK.CSharp.Proxy;

using OpenTracing;
using Wavefront.SDK.CSharp.DirectIngestion;
using System.Reflection;

namespace wavefront_sdk
{
    public static class SteeltoeWavefrontProxyExtensions
    {
        public abstract class WavefrontCredentails
        {
            public WavefrontCredentails()
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
        public class WavefrontProxyOptions : WavefrontCredentails
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

        public class WavefrontDirectIngestionOptions : WavefrontCredentails
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

        public static IServiceCollection AddWavefrontProxy(this IServiceCollection services, IConfiguration configuration)
        {
            var waveFrontProxyConfiguration = 
                configuration.GetSection(WavefrontProxyOptions.WavefrontProxy).Get<WavefrontProxyOptions>();

            var wfProxyClientBuilder = new WavefrontProxyClient.Builder(waveFrontProxyConfiguration.Hostname);
            wfProxyClientBuilder.MetricsPort(waveFrontProxyConfiguration.Port);
            wfProxyClientBuilder.DistributionPort(waveFrontProxyConfiguration.DistributionPort);
            wfProxyClientBuilder.TracingPort(waveFrontProxyConfiguration.TracingPort);
            wfProxyClientBuilder.FlushIntervalSeconds(waveFrontProxyConfiguration.FlushIntervalSeconds);
            var wavefrontSender = wfProxyClientBuilder.Build();

            var applicationTags = new ApplicationTags.Builder(waveFrontProxyConfiguration.Application, waveFrontProxyConfiguration.Service)
              .Cluster(waveFrontProxyConfiguration.Cluster)
              .Shard(waveFrontProxyConfiguration.Shard)
              .Build();

            var wfAspNetCoreReporter = new WavefrontAspNetCoreReporter.Builder(applicationTags)
                .WithSource(waveFrontProxyConfiguration.Source)
                .ReportingIntervalSeconds(waveFrontProxyConfiguration.ReportingIntervalSeconds)
                .Build(wavefrontSender);

            var wavefrontSpanReporter = new WavefrontSpanReporter.Builder()
              .Build(wavefrontSender);

            ITracer tracer = new WavefrontTracer.Builder(wavefrontSpanReporter, applicationTags).Build();

            services.AddWavefrontForMvc(wfAspNetCoreReporter, tracer);

            return services;
        }

        public static IServiceCollection AddWavefrontDirectIngestion(this IServiceCollection services, IConfiguration configuration)
        {
            var waveFrontDirectIngestionConfiguration = 
                configuration.GetSection(WavefrontDirectIngestionOptions.WavefrontDirectIngestion)
                .Get<WavefrontDirectIngestionOptions>();

            var allPublicFields = typeof(WavefrontDirectIngestionOptions).
                GetFields(BindingFlags.Public | BindingFlags.Instance );

            foreach (var field in allPublicFields)
            {
                System.Console.WriteLine($"{field.Name}{field.GetValue(waveFrontDirectIngestionConfiguration).ToString()}");
            }

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