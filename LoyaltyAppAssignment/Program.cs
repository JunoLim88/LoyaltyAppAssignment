
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<LoyaltyAppAssignment.Services.AdminApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LoyaltyApi:BaseUrl"] ?? "https://alpacaapi.xcode.com.my/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Enable Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
