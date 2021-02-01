using EntityFrameworkCore.TemporalTables.Extensions;
using EntityFrameworkCore.TemporalTables.Sql;
using EntityFrameworkCore.TemporalTables.TestApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;

namespace EntityFrameworkCore.TemporalTables.TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json")
              .AddUserSecrets(typeof(Program).Assembly)
              .Build();

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<Tests>();

            services.AddDbContextPool<DataContext>((provider, options) =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.UseInternalServiceProvider(provider);
            });
            
            services.AddEntityFrameworkSqlServer();

            services.RegisterTemporalTablesForDatabase<DataContext>();

            var serviceProvider = services
                .BuildServiceProvider();

            var dbContext = serviceProvider.GetService<DataContext>();

            // Update temporal tables automatically by calling Migrate() / MigrateAsync() or Update-Database from Package Manager Console.
            dbContext.Database.Migrate();

            // Just generate the temporal tables SQL without executing it against the database.
            var temporalTableSqlBuilder = serviceProvider.GetService<ITemporalTableSqlBuilder<DataContext>>();
            string sql = temporalTableSqlBuilder.BuildTemporalTablesSql();

            // Execute the temporal tables SQL code against the database (SQL code is generated internally without exposing to outside).
            var temporalTableSqlExecutor = serviceProvider.GetService<ITemporalTableSqlExecutor<DataContext>>();
            temporalTableSqlExecutor.Execute();

            serviceProvider.GetService<Tests>().RunTests();
        }
    }

    //TODO: move tests into test projects
    public class Tests
    {
        private IServiceProvider Services { get; set; }
        private DataContext DbContext { get; set; }

        public Tests(IServiceProvider services, DataContext dbContext)
        {
            Services = services;
            DbContext = dbContext;
        }

        public void RunTests()
        {
            Test1();
        }

        public void Test1()
        {
            DbContext.Users.RemoveRange(DbContext.Users);
            DbContext.SaveChanges();
            string firstPassword = "StrongPassword1";
            var user = new User();
            user.UserName = "NewUser1";
            user.Password = firstPassword;

            DbContext.Add(user);
            DbContext.SaveChanges();

            DateTime firstTime = DateTime.Now.ToUniversalTime();

            System.Threading.Thread.Sleep(1000);
            user.Password = "StrongerPassword123";

            DbContext.SaveChanges();

            var oldUser = DbContext.Users.AsOf(firstTime).FirstOrDefault(u => u.Id == user.Id);
            if(oldUser is null || !string.Equals(oldUser.Password, firstPassword))
            {
                throw new Exception("Something's broken!");
            }

            var userHistory = DbContext.Users.ForAll();
            Console.WriteLine(userHistory.Count());
        }
    }
}
