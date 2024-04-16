using SignalingServerMvp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
       //.AddStackExchangeRedis();

builder.Services.AddCors(options =>
{
   options.AddDefaultPolicy(
       builder =>
       {
           builder.AllowAnyOrigin();
           builder.AllowAnyHeader();
           builder.AllowAnyMethod();
       });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRouting();
app.MapHub<SignalingHub>("/signals");
app.Run();