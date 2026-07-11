#:sdk Aspire.AppHost.Sdk@13.4.6+87fe259e4fc244c599019a7b1304c85a1488f248

var builder = DistributedApplication.CreateBuilder(args);

// The aspireify skill will wire up your projects here.

builder.Build().Run();