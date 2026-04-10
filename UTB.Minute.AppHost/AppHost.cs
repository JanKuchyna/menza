var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent))
                      .WithDataVolume()
                      .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("database");

builder.AddProject<Projects.UTB_Minute_DbManager>("utb-minute-dbmanager")
       .WithReference(database)
       .WithHttpCommand("reset-db", "Reset Database")
       .WaitFor(database); 

builder.AddProject<Projects.UTB_Minute_WebApi>("utb-minute-webapi")
       .WithReference(database)
       .WaitFor(database);

builder.Build().Run();