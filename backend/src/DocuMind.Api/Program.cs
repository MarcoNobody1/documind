using DocuMind.Api.Endpoints;
using DocuMind.Application;
using DocuMind.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Retrieval:TopK is a tuning knob, not a secret, so it ships as a checked-in default in
// appsettings.json (see the Retrieval section) rather than through user-secrets. The Application
// layer takes it as a plain int so it stays unaware of the configuration source; only this
// composition root reads it.
var retrievalTopK = builder.Configuration.GetValue("Retrieval:TopK", DocuMind.Application.DependencyInjection.DefaultRetrievalTopK);
builder.Services.AddApplication(retrievalTopK);

builder.Services.AddInfrastructure(builder.Configuration);

const string AngularClientCorsPolicy = "AngularClient";
builder.Services.AddCors(options =>
{
    // The Angular dev server and the chat endpoint's SSE stream are consumed cross-origin; no
    // auth cookie exists to protect, so an open policy scoped to the known dev origin is enough
    // for the Phase 1 MVP. Revisit once a deployed client origin is known.
    options.AddPolicy(AngularClientCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(AngularClientCorsPolicy);

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");

app.MapDocumentsEndpoints();
app.MapChatEndpoints();

app.Run();
