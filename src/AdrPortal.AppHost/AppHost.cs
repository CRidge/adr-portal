var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AdrPortal_Web>("web")
    .WithExternalHttpEndpoints()
    .WithEnvironment("ConnectionStrings__AdrPortal", "Data Source=adrportal.db");

builder.Build().Run();
