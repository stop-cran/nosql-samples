using System.Collections.Generic;
using Cassandra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using WebApplication1.Awaitable;
using WebApplication1.Repositories;
using ICluster = MongoDB.Driver.Core.Clusters.ICluster;

namespace WebApplication1
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton<IMongoClient>(_ =>
                    new MongoClient(Configuration["MongoDbWeather:ConnectionString"]))
                .AddSingleton(serviceProvider =>
                    serviceProvider.GetRequiredService<IMongoClient>()
                        .GetDatabase(Configuration["MongoDbWeather:Database"]))
                .AddSingleton(serviceProvider =>
                    serviceProvider.GetRequiredService<IMongoDatabase>()
                        .GetCollection<WeatherDto>("weather"))
                .AddScoped(serviceProvider =>
                    serviceProvider.GetRequiredService<IMongoClient>()
                        .StartSession());
            services.AddControllers();

            services.AddTransient<IWeatherRepository, MongoDbWeatherRepository>();
            services.AddTransient<IUniqueResource, UniqueResource>();

            services.AddSingletonTaskAwaitable<IConnectionMultiplexer>(async _ =>
                    await ConnectionMultiplexer.ConnectAsync(Configuration["Redis:Configuration"]))
                .AddSingletonTaskAwaitable<IDistributedLockFactory>(async serviceProvider =>
                    RedLockFactory.Create(new List<RedLockMultiplexer>
                    {
                        new(await serviceProvider.GetRequiredService<ITaskAwaitable<IConnectionMultiplexer>>())
                    }));

            services.AddSingleton<Cassandra.ICluster>(_ =>
                    Cluster.Builder()
                        .AddContactPoint(Configuration["Cassandra:Host"])
                        .Build())
                .AddScopedTaskAwaitable(async serviceProvider =>
                    await serviceProvider.GetRequiredService<Cassandra.ICluster>().ConnectAsync());

            services
                .AddLogging(loggingBuilder =>
                    loggingBuilder.AddLog4Net("Environment/log4net.config"))
                .AddHostedService<LogFlushService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}