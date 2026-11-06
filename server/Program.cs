using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JournalStore>();
builder.Services.AddSingleton<AuthTokens>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
app.MapPost("/api/auth/register", (Credentials credentials, JournalStore store, AuthTokens tokens) => {
    if (string.IsNullOrWhiteSpace(credentials.DisplayName) || credentials.DisplayName.Trim().Length > 80 || credentials.Email is null || credentials.Password is null || !ValidEmail(credentials.Email) || credentials.Password.Length < 8 || credentials.Password.Length > 128)
        return Results.BadRequest(new { message = "Enter a display name, valid email, and password of at least 8 characters." });
    var email = credentials.Email.Trim().ToLowerInvariant();
    if (!store.CreateUser(email, credentials.DisplayName.Trim(), Hash(credentials.Password))) return Results.Conflict(new { message = "An account with that email already exists." });
    var token = tokens.Issue(email);
    return Results.Ok(new AuthResponse(token, store.GetState(email)!));
});
app.MapPost("/api/auth/login", (LoginRequest request, JournalStore store, AuthTokens tokens) => {
    if (request.Email is null || request.Password is null || !ValidEmail(request.Email) || request.Password.Length > 128) return Results.Unauthorized();
    var email = request.Email.Trim().ToLowerInvariant();
    var hash = store.GetPasswordHash(email);
    if (hash is null || !VerifyPassword(request.Password, hash)) return Results.Unauthorized();
    return Results.Ok(new AuthResponse(tokens.Issue(email), store.GetState(email)!));
});
app.MapPost("/api/auth/logout", (HttpContext context, AuthTokens tokens) => { tokens.Revoke(Bearer(context)); return Results.NoContent(); });
app.MapGet("/api/state", (HttpContext context, JournalStore store, AuthTokens tokens) => {
    var email = tokens.GetUser(Bearer(context)); return email is null ? Results.Unauthorized() : Results.Ok(store.GetState(email));
});
app.MapPut("/api/state", (HttpContext context, AppState state, JournalStore store, AuthTokens tokens) => {
    var email = tokens.GetUser(Bearer(context));
    if (email is null) return Results.Unauthorized();
    if (state is null || state.Profile is null || state.Meals is null || state.Water is null || string.IsNullOrWhiteSpace(state.Profile.DisplayName) || state.Profile.DisplayName.Length > 80 || state.Profile.Email is null || state.Profile.TimeZone is null || state.Meals.Count > 20000 || state.Water.Count > 20000 || state.Profile.CalorieGoal is < 0 or > 10000 || state.Profile.WaterGoal is <= 0 or > 20 || state.Profile.CarbsGoal + state.Profile.ProteinGoal + state.Profile.FatGoal != 100 || new[] { state.Profile.CarbsGoal, state.Profile.ProteinGoal, state.Profile.FatGoal }.Any(x => x < 0 || x > 100))
        return Results.BadRequest(new { message = "Journal data or profile goals are invalid." });
    if (state.Meals.Any(m => m is null || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 160 || m.Portions is < 0.1 or > 100 || m.Calories is < 0 or > 100000 || new[] { m.Carbs, m.Protein, m.Fat }.Any(x => x is < 0 or > 10000) || m.PortionLabel is null || m.PortionLabel.Length > 30 || m.Category is null || m.Type is null || !Enum.TryParse<FoodCategory>(m.Category, true, out _) || !DateTime.TryParse(m.EatenAt, out _)) || state.Water.Any(w => w is null || w.Litres is <= 0 or > 10 || !DateTime.TryParse(w.LoggedAt, out _)))
        return Results.BadRequest(new { message = "One or more food or water entries are invalid." });
    store.SaveState(email, state); return Results.Ok(state);
});
app.Run();

static string Hash(string password) { var salt = RandomNumberGenerator.GetBytes(16); var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32); return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(derived)}"; }
static bool VerifyPassword(string password, string stored) { try { var parts = stored.Split(':', 2); if (parts.Length != 2) return false; var expected = Convert.FromBase64String(parts[1]); var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[0]), 210_000, HashAlgorithmName.SHA256, 32); return CryptographicOperations.FixedTimeEquals(expected, actual); } catch (FormatException) { return false; } }
static bool ValidEmail(string email) => email.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(email, out var address) && address.Address == email;
static string? Bearer(HttpContext context) => context.Request.Headers.Authorization.ToString() is var value && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : null;

record Credentials(string DisplayName, string Email, string Password);
record LoginRequest(string Email, string Password);
record AuthResponse(string Token, AppState State);
enum FoodCategory { Breakfast, Lunch, Dinner, Snack, Drink }
record MealDto(string Id, string Name, string Category, string Type, string EatenAt, double Portions, string PortionLabel, double? Calories, double? Carbs, double? Protein, double? Fat);
record WaterDto(string Id, string LoggedAt, double Litres);
record ProfileDto(string DisplayName, string Email, string TimeZone, double CalorieGoal, double WaterGoal, int CarbsGoal, int ProteinGoal, int FatGoal, bool Reminders, bool WeeklySummary);
record AppState(ProfileDto Profile, List<MealDto> Meals, List<WaterDto> Water);

sealed class AuthTokens {
    private readonly ConcurrentDictionary<string, string> _tokens = new();
    public string Issue(string email) { var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36)); _tokens[token] = email; return token; }
    public string? GetUser(string? token) => token is not null && _tokens.TryGetValue(token, out var email) ? email : null;
    public void Revoke(string? token) { if (token is not null) _tokens.TryRemove(token, out _); }
}
sealed class JournalStore {
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(Directory.GetCurrentDirectory(), "food-journal-data.json");
    private Dictionary<string, StoredUser> _users;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public JournalStore() { try { _users = JsonSerializer.Deserialize<Dictionary<string, StoredUser>>(File.ReadAllText(_file), Options) ?? new(); } catch { _users = new(); } }
    public bool CreateUser(string email, string name, string hash) { lock (_gate) { if (_users.ContainsKey(email)) return false; _users[email] = new(hash, new(new(name, email, "UTC", 2000, 2.5, 45, 30, 25, true, true), new(), new())); Persist(); return true; } }
    public string? GetPasswordHash(string email) { lock (_gate) return _users.TryGetValue(email, out var user) ? user.PasswordHash : null; }
    public AppState? GetState(string email) { lock (_gate) return _users.TryGetValue(email, out var user) ? user.State : null; }
    public void SaveState(string email, AppState state) { lock (_gate) { if (_users.TryGetValue(email, out var user)) { var profile = state.Profile with { Email = email }; _users[email] = user with { State = state with { Profile = profile } }; Persist(); } } }
    private void Persist() { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); var temp = _file + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(_users, Options)); File.Move(temp, _file, true); }
    private sealed record StoredUser(string PasswordHash, AppState State);
}
