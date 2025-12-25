using ESC_GULEN_OPTIK_Web.Data;
using ESC_GULEN_OPTIK_Web.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register DBConnection as Scoped (like instructor's pattern)
builder.Services.AddScoped<DBConnection>();

// Register authorization filters
builder.Services.AddScoped<AuthenticationFilter>();
builder.Services.AddScoped<AdminAuthorizationFilter>();

// Add session support for login
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".ESCGulenOptik.Session";
});

// Add HttpContextAccessor for accessing session in views
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Test database connection on startup
using (var scope = app.Services.CreateScope())
{
    var dbcon = scope.ServiceProvider.GetRequiredService<DBConnection>();
    var (success, message) = dbcon.TestConnection();
    
    Console.WriteLine("============================================");
    if (success)
    {
        Console.WriteLine("DATABASE CONNECTION SUCCESSFUL!");
        Console.WriteLine(message);
    }
    else
    {
        Console.WriteLine("DATABASE CONNECTION FAILED!");
        Console.WriteLine(message);
    }
    Console.WriteLine("============================================");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session middleware (must be before UseAuthorization)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
