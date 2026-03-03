using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Configuration for OmniNet server connections.
    /// </summary>
    public class OmniNetConfig
    {
        private static OmniNetConfig _instance;
        private static readonly object _lock = new object();

        public ServerUrlsConfig ServerUrls { get; set; } = new ServerUrlsConfig();
        public SettingsConfig Settings { get; set; } = new SettingsConfig();

        // Local user session data (not saved to config file)
        [JsonIgnore]
        public string SecretKey { get; set; }
        [JsonIgnore]
        public string Nickname { get; set; }
        [JsonIgnore]
        public string UserEmail { get; set; }
        [JsonIgnore]
        public string DeviceId { get; set; } // Add DeviceId property
        [JsonIgnore]
        public bool IsLoggedIn => !string.IsNullOrEmpty(SecretKey);

        public static OmniNetConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the appropriate server URL based on environment.
        /// Tries localhost first (for debugging), then falls back to staging/production.
        /// </summary>
        public string GetServerUrl()
        {
#if DEBUG
            // In debug mode, try localhost first
            if (IsServerAvailable(ServerUrls.Development))
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Using Development server: {ServerUrls.Development}");
                return ServerUrls.Development;
            }
            // Fallback to staging in debug
            System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Development server unavailable, using Staging: {ServerUrls.Staging}");
            return ServerUrls.Staging;
#else
            // In release mode, use production
            System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Using Production server: {ServerUrls.Production}");
            return ServerUrls.Production;
#endif
        }

        /// <summary>
        /// Quick check if a server is available.
        /// </summary>
        private bool IsServerAvailable(string url)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    // Increase timeout to 5 seconds for slower local servers
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    // Use synchronous approach properly
                    var task = client.GetAsync($"{url}/health");
                    
                    // Wait with longer timeout
                    if (task.Wait(TimeSpan.FromSeconds(5)))
                    {
                        var response = task.Result;
                        bool isAvailable = response.IsSuccessStatusCode;
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Server {url} availability check: {isAvailable} (Status: {response.StatusCode})");
                        return isAvailable;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Server {url} timed out after 5 seconds");
                        return false;
                    }
                }
            }
            catch (AggregateException ae)
            {
                // Flatten and check for specific exceptions
                var innerException = ae.Flatten().InnerException;
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Server {url} unavailable: {innerException?.Message ?? ae.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Server {url} unavailable: {ex.Message}");
                return false;
            }
        }

        private static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appData, "OmnipetModuleEditor");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, "omninet_config.json");
        }

        private static string GetSessionPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appData, "OmnipetModuleEditor");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, "session.json");
        }

        public static OmniNetConfig Load()
        {
            var config = new OmniNetConfig();
            string configPath = GetConfigPath();
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<OmniNetConfig>(json);
                }
                catch
                {
                    // If config is corrupted, use defaults
                    config = new OmniNetConfig();
                }
            }

            // Load session data separately
            config.LoadSession();
            
            return config;
        }

        public void Save()
        {
            try
            {
                string configPath = GetConfigPath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        // Update SaveSession to save device_id with more detailed logging
        public void SaveSession()
        {
            try
            {
                var sessionPath = GetSessionPath();
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] SaveSession called");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session path: {sessionPath}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] SecretKey: {SecretKey?.Substring(0, Math.Min(10, SecretKey?.Length ?? 0))}...");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] DeviceId: {DeviceId}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Nickname: {Nickname}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] UserEmail: {UserEmail}");
                
                var sessionData = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "SecretKey", SecretKey ?? "" },
                    { "DeviceId", DeviceId ?? "" },
                    { "Nickname", Nickname ?? "" },
                    { "UserEmail", UserEmail ?? "" }
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(sessionData);
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] JSON to save: {json}");
                
                File.WriteAllText(sessionPath, json);
                
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session file written successfully");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] File exists after write: {File.Exists(sessionPath)}");
                
                if (File.Exists(sessionPath))
                {
                    var savedContent = File.ReadAllText(sessionPath);
                    System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Saved file content: {savedContent}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Failed to save session: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Stack trace: {ex.StackTrace}");
            }
        }

        // Update LoadSession to load device_id with more detailed logging
        private void LoadSession()
        {
            try
            {
                string sessionPath = GetSessionPath();
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] LoadSession called");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session path: {sessionPath}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session file exists: {File.Exists(sessionPath)}");
                
                if (File.Exists(sessionPath))
                {
                    var json = File.ReadAllText(sessionPath);
                    System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session file content: {json}");
                    
                    var sessionData = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                    
                    if (sessionData != null)
                    {
                        if (sessionData.ContainsKey("SecretKey"))
                            SecretKey = sessionData["SecretKey"];
                        if (sessionData.ContainsKey("DeviceId"))
                            DeviceId = sessionData["DeviceId"];
                        if (sessionData.ContainsKey("Nickname"))
                            Nickname = sessionData["Nickname"];
                        if (sessionData.ContainsKey("UserEmail"))
                            UserEmail = sessionData["UserEmail"];
                            
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Session loaded successfully");
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] SecretKey exists: {!string.IsNullOrEmpty(SecretKey)}");
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] DeviceId: {DeviceId}");
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Nickname: {Nickname}");
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] UserEmail: {UserEmail}");
                        System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] IsLoggedIn: {IsLoggedIn}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[OmniNetConfig] Session data deserialized to null");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[OmniNetConfig] No session file found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Failed to load session: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Stack trace: {ex.StackTrace}");
            }
        }

        // Update ClearSession to clear device_id
        public void ClearSession()
        {
            SecretKey = null;
            DeviceId = null;
            Nickname = null;
            UserEmail = null;
            
            try
            {
                string sessionPath = GetSessionPath();
                if (File.Exists(sessionPath))
                {
                    File.Delete(sessionPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetConfig] Failed to delete session file: {ex.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine("[OmniNetConfig] Session cleared");
        }

        private class SessionData
        {
            public string SecretKey { get; set; }
            public string Nickname { get; set; }
            public string Email { get; set; }
        }
    }

    public class ServerUrlsConfig
    {
        public string Development { get; set; } = "http://localhost:8000";
        public string Staging { get; set; } = "https://dev.omnipet.app.br";
        public string Production { get; set; } = "https://omnipet.app.br";
    }

    public class SettingsConfig
    {
        public int CodeExpirationMinutes { get; set; } = 5;
        public int MaxRetryAttempts { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
