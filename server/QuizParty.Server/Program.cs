using QuizParty.Server.Game;
using QuizParty.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration.GetValue("Quiz:Port", 5000);
// Écoute sur toutes les cartes réseau : les téléphones du Wi-Fi de la maison peuvent se connecter.
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSignalR();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LanAddress>();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<QuizHub>("/hub");
app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var lan = app.Services.GetRequiredService<LanAddress>();
    Console.WriteLine();
    Console.WriteLine("  Quiz Party est lancé !");
    Console.WriteLine($"  Écran animateur (sur ce PC) : http://localhost:{port}/host");
    Console.WriteLine($"  Joueurs (même Wi-Fi)         : {lan.BaseUrl}/play");
    Console.WriteLine();
});

app.Run();
