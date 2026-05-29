var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent))
                      .WithDataVolume()
                      .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("database");

var keycloak = builder.AddKeycloak("keycloak", 8080)
                      .WithDataVolume()
                      .WithLifetime(ContainerLifetime.Persistent)
                      .WithRealmImport(Path.Combine(builder.AppHostDirectory, "KeycloakImport"));

builder.AddProject<Projects.UTB_Minute_DbManager>("utb-minute-dbmanager")
       .WithReference(database)
       .WithHttpCommand("reset-db", "Reset Database")
       .WaitFor(database);

var webapi = builder.AddProject<Projects.UTB_Minute_WebApi>("utb-minute-webapi")
       .WithReference(database)
       .WithReference(keycloak)
       .WaitFor(database)
       .WaitFor(keycloak);

builder.AddProject<Projects.UTB_Minute_AdminClient>("utb-minute-adminclient")
       .WithReference(webapi)
       .WithReference(keycloak)
       .WaitFor(keycloak)
       .WithExternalHttpEndpoints();

builder.AddProject<Projects.UTB_Minute_CanteenClient>("utb-minute-canteenclient")
       .WithReference(webapi)
       .WithReference(keycloak)
       .WaitFor(keycloak)
       .WithExternalHttpEndpoints();

builder.Build().Run();
