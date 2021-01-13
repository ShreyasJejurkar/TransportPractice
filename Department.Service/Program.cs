﻿using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Department.Components.Consumers;
using Department.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharedFramework;

namespace Department.Service
{
	public class Program
	{
		public static AppConfig AppConfig { get; set; }

		private static async Task Main(string[] args)
		{
			var isService = !(Debugger.IsAttached || args.Contains("--console"));

			Log.Logger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.CreateLogger();

			var builder = new HostBuilder()
				.ConfigureAppConfiguration((_, config) =>
				{
					config.AddJsonFile("appsettings.json", true);
					config.AddEnvironmentVariables();

					if (args != null)
						config.AddCommandLine(args);
				})
				.ConfigureServices((hostContext, services) =>
				{
					AppConfig = new AppConfig();
					hostContext.Configuration.GetSection("AppConfig").Bind(AppConfig);

					services.AddAutoMapper(typeof(CreateDepartmentConsumer).Assembly);

					services.AddMassTransit(config =>
					{
						config.AddConsumersFromNamespaceContaining(typeof(CreateDepartmentConsumer));

						config.UsingRabbitMq((ctx, cfg) =>
						{
							cfg.Host(AppConfig.Host);
							cfg.ConfigureEndpoints(ctx);
						});
					});

					services.AddDbContext<DepartmentDbContext>(
					option =>
					{
						option.UseSqlServer(AppConfig.ConnectionString, a =>
						{
							a.MigrationsAssembly(typeof(DepartmentDbContext).FullName);
						});
					});

					services.AddHostedService<MassTransitConsoleHostedService>();
				}).UseSerilog()
				.ConfigureLogging((hostingContext, logging) =>
				{
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
					logging.AddConsole();
				});

			if (isService)
			{
				//await builder.UseWindowsService().Build().RunAsync();
				//await builder.UseSystemd().Build().RunAsync(); // For Linux, replace the nuget package: "Microsoft.Extensions.Hosting.WindowsServices" with "Microsoft.Extensions.Hosting.Systemd", and then use this line instead
			}
			else
			{
				await builder.RunConsoleAsync();
			}

			Log.Logger.Write(LogEventLevel.Debug, "Employee Service has been started...");
		}
	}
}