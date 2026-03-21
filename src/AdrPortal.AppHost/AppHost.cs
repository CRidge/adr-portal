var builder = DistributedApplication.CreateBuilder(args);
var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var persistenceRootPath = string.IsNullOrWhiteSpace(localApplicationDataPath)
    ? Path.Combine(
        string.IsNullOrWhiteSpace(userProfilePath) ? AppContext.BaseDirectory : userProfilePath,
        ".adrportal",
        "data")
    : Path.Combine(localApplicationDataPath, "AdrPortal", "data");

builder.AddProject<Projects.AdrPortal_Web>("web")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Persistence__DatabaseRootPath", persistenceRootPath);

builder.Build().Run();
