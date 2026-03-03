using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// API client for OmniNet server communication.
    /// </summary>
    public class OmniNetApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly OmniNetConfig _config;
        private string _deviceKey;

        public OmniNetApiClient()
        {
            _config = OmniNetConfig.Instance;
            
            // Create HttpClientHandler to configure SSL/TLS settings
            var handler = new HttpClientHandler();
            
#if DEBUG
            // In debug mode, accept all SSL certificates (including self-signed)
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            System.Diagnostics.Debug.WriteLine("[OmniNetApiClient] DEBUG mode - SSL certificate validation disabled");
#endif
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_config.Settings.TimeoutSeconds)
            };
        }

        private string BaseUrl => _config.GetServerUrl();

        /// <summary>
        /// Set the device key for authenticated requests.
        /// </summary>
        public void SetDeviceKey(string deviceKey)
        {
            _deviceKey = deviceKey;
        }

        #region Account Endpoints

        /// <summary>
        /// Create a new user account. Returns success and a message.
        /// </summary>
        public async Task<ApiResponse<CreateAccountResponse>> CreateAccountAsync(string nickname, string email, string password)
        {
            var request = new
            {
                nickname = nickname,
                email = email,
                password = password
            };
            return await PostAsync<CreateAccountResponse>("/api/v1/auth/register", request);
        }

        /// <summary>
        /// Verify email with the 6-character code.
        /// </summary>
        public async Task<ApiResponse<VerifyCodeResponse>> VerifyEmailCodeAsync(string email, string code)
        {
            var request = new
            {
                email = email,
                code = code
            };
            return await PostAsync<VerifyCodeResponse>("/api/v1/auth/verify-registration", request);
        }

        /// <summary>
        /// Resend verification code.
        /// </summary>
        public async Task<ApiResponse<BaseResponse>> ResendVerificationCodeAsync(string email)
        {
            try
            {
                var fullUrl = $"{BaseUrl}/api/v1/auth/resend-code?email={Uri.EscapeDataString(email)}";
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] POST {fullUrl}");
                
                var response = await _httpClient.PostAsync(fullUrl, new StringContent(""));
                var responseJson = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Response status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Response body: {responseJson.Substring(0, Math.Min(500, responseJson.Length))}...");

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var result = JsonSerializer.Deserialize<BaseResponse>(responseJson, options);
                    return new ApiResponse<BaseResponse> { Success = true, Data = result };
                }
                else
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var error = JsonSerializer.Deserialize<ErrorResponse>(responseJson, options);
                    return new ApiResponse<BaseResponse>
                    {
                        Success = false,
                        ErrorMessage = error?.GetDetail() ?? $"Server returned {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Resend error: {ex.Message}");
                return new ApiResponse<BaseResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Login with email and password. Sends verification code.
        /// </summary>
        public async Task<ApiResponse<LoginResponse>> LoginAsync(string email, string password)
        {
            var request = new
            {
                email = email,
                password = password
            };
            return await PostAsync<LoginResponse>("/api/v1/auth/login", request);
        }

        /// <summary>
        /// Confirm login with verification code.
        /// </summary>
        public async Task<ApiResponse<ConfirmLoginResponse>> ConfirmLoginAsync(string email, string code, bool clearDevices = false)
        {
            var request = new
            {
                email = email,
                code = code,
                clear_devices = clearDevices
            };
            return await PostAsync<ConfirmLoginResponse>("/api/v1/auth/verify-login", request);
        }

        /// <summary>
        /// Validate a secret key for auto-login.
        /// </summary>
        public async Task<ApiResponse<ValidateKeyResponse>> ValidateSecretKeyAsync(string secretKey)
        {
            System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] ValidateSecretKeyAsync called");
            System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Adding X-Device-Key header with secret key");
            
            // Add secret key to header for authentication
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);
            
            // Verify header was added
            if (_httpClient.DefaultRequestHeaders.Contains("X-Device-Key"))
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] X-Device-Key header added successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] WARNING: X-Device-Key header NOT added!");
            }
            
            // Empty request body since authentication is via header
            var response = await PostAsync<ValidateKeyResponse>("/api/v1/auth/validate-device", new { });
            
            // Clear header after request
            System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Removing X-Device-Key header");
            _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
            
            return response;
        }

        /// <summary>
        /// Generate a 4-character code for linking a game device.
        /// </summary>
        public async Task<ApiResponse<GenerateGameCodeResponse>> GenerateGameLinkCodeAsync(string secretKey)
        {
            // Add authentication header
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);
            
            // Empty request body since authentication is via header
            var response = await PostAsync<GenerateGameCodeResponse>("/api/v1/auth/generate-pairing-code", new { });
            
            // Clear header
            _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
            
            return response;
        }

        /// <summary>
        /// Logout and invalidate the secret key.
        /// </summary>
        public async Task<ApiResponse<BaseResponse>> LogoutAsync(string secretKey)
        {
            var request = new { secret_key = secretKey };
            return await PostAsync<BaseResponse>("/api/v1/auth/logout", request);
        }

        #endregion

        #region Module Endpoints

        /// <summary>
        /// Get the status of a module by ID.
        /// </summary>
        public async Task<ApiResponse<ModuleStatusResponse>> GetModuleStatusAsync(string moduleId, string secretKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/v1/modules/{moduleId}");
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ModuleStatusResponse>(json);
                    return new ApiResponse<ModuleStatusResponse> { Success = true, Data = result };
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(json);
                    return new ApiResponse<ModuleStatusResponse>
                    {
                        Success = false,
                        ErrorMessage = error?.GetDetail() ?? $"Server returned {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ModuleStatusResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Check if a module name is available or belongs to the user.
        /// </summary>
        public async Task<ApiResponse<CheckModuleResponse>> CheckModuleAsync(string moduleName, string version, string secretKey)
        {
            var request = new
            {
                name = moduleName,
                version = version
            };
            
            // Add authentication header
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);
            
            var response = await PostAsync<CheckModuleResponse>("/api/v1/modules/check-publish", request);
            
            // Clear header
            _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
            
            return response;
        }

        /// <summary>
        /// Publish or update a module by uploading a zip file.
        /// </summary>
        public async Task<ApiResponse<PublishModuleResponse>> PublishModuleAsync(string secretKey, string modulePath)
        {
            try
            {
                // Create zip file
                string tempZipPath = Path.GetTempFileName() + ".zip";
                try
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(modulePath, tempZipPath);

                    using (var content = new MultipartFormDataContent())
                    {
                        var fileBytes = File.ReadAllBytes(tempZipPath);
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/zip");
                        content.Add(fileContent, "file", Path.GetFileName(modulePath) + ".zip");

                        // Add authentication header
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);

                        var response = await _httpClient.PostAsync($"{BaseUrl}/api/v1/modules/publish", content);
                        var json = await response.Content.ReadAsStringAsync();

                        // Clear header
                        _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");

                        if (response.IsSuccessStatusCode)
                        {
                            var result = JsonSerializer.Deserialize<PublishModuleResponse>(json);
                            return new ApiResponse<PublishModuleResponse> { Success = true, Data = result };
                        }
                        else
                        {
                            var error = JsonSerializer.Deserialize<ErrorResponse>(json);
                            return new ApiResponse<PublishModuleResponse> 
                            { 
                                Success = false, 
                                ErrorMessage = error?.GetDetail() ?? "Failed to publish module" 
                            };
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempZipPath))
                        File.Delete(tempZipPath);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<PublishModuleResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Unpublish a module.
        /// </summary>
        public async Task<ApiResponse<BaseResponse>> UnpublishModuleAsync(string moduleId, string secretKey)
        {
            // Add authentication header with secret key
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);
            
            var response = await PostAsync<BaseResponse>($"/api/v1/modules/{moduleId}/unpublish", new { });
            
            // Clear header
            _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
            
            return response;
        }

        /// <summary>
        /// Get contributors for a module.
        /// </summary>
        public async Task<ApiResponse<ContributorsResponse>> GetContributorsAsync(string moduleId, string secretKey)
        {
            try
            {
                // Add authentication header
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);

                var response = await _httpClient.GetAsync($"{BaseUrl}/api/v1/modules/{moduleId}/contributors");
                var json = await response.Content.ReadAsStringAsync();

                // Clear header
                _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ContributorsResponse>(json);
                    return new ApiResponse<ContributorsResponse> { Success = true, Data = result };
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(json);
                    return new ApiResponse<ContributorsResponse>
                    {
                        Success = false,
                        ErrorMessage = error?.GetDetail() ?? $"Server returned {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ContributorsResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Update contributors for a module.
        /// </summary>
        public async Task<ApiResponse<BaseResponse>> UpdateContributorsAsync(string moduleId, string secretKey, List<string> contributors)
        {
            var request = new
            {
                nicknames = contributors
            };
            
            // Add authentication header
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Key", secretKey);
            
            var response = await PostAsync<BaseResponse>($"/api/v1/modules/{moduleId}/contributors", request);
            
            // Clear header
            _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
            
            return response;
        }

        #endregion

        #region Helper Methods

        private async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data) where T : class
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                var json = JsonSerializer.Serialize(data, options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var fullUrl = $"{BaseUrl}{endpoint}";
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] POST {fullUrl}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Request body: {json}");
                
                var response = await _httpClient.PostAsync(fullUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Response status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Response body: {responseJson.Substring(0, Math.Min(500, responseJson.Length))}...");

                if (response.IsSuccessStatusCode)
                {
                    // Check if response is HTML instead of JSON
                    if (responseJson.TrimStart().StartsWith("<"))
                    {
                        System.Diagnostics.Debug.WriteLine("[OmniNetApiClient] ERROR: Server returned HTML instead of JSON");
                        return new ApiResponse<T>
                        {
                            Success = false,
                            ErrorMessage = "Server returned HTML instead of JSON. The API endpoint may not be correctly configured."
                        };
                    }
                    
                    try
                    {
                        var result = JsonSerializer.Deserialize<T>(responseJson, options);
                        return new ApiResponse<T> { Success = true, Data = result };
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] JSON deserialization error: {jsonEx.Message}");
                        return new ApiResponse<T>
                        {
                            Success = false,
                            ErrorMessage = $"Invalid JSON response from server: {jsonEx.Message}"
                        };
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Error response, attempting to parse...");
                    
                    try
                    {
                        // First try to deserialize as error response
                        var error = JsonSerializer.Deserialize<ErrorResponse>(responseJson, options);
                        string errorMessage = error?.GetDetail() ?? $"Server returned {response.StatusCode}";
                        
                        System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Error message extracted: {errorMessage}");
                        
                        return new ApiResponse<T>
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                    }
                    catch (Exception deserializeEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Failed to deserialize error: {deserializeEx.Message}");
                        // If error response is not JSON, return the status code and raw response
                        return new ApiResponse<T>
                        {
                            Success = false,
                            ErrorMessage = $"Server returned {response.StatusCode}: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}"
                        };
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Request timed out: {ex.Message}");
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Request timed out. Please check your connection."
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Network error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Inner exception: {ex.InnerException.Message}");
                }
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OmniNetApiClient] Unexpected error: {ex.GetType().Name} - {ex.Message}");
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #endregion
    }

    #region Response Models

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BaseResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class ErrorResponse
    {
        public string Detail { get; set; }
        public string error { get; set; }
        public string message { get; set; }
        
        public string GetDetail()
        {
            // Try all possible error field names
            return Detail ?? error ?? message ?? "Unknown error";
        }
    }

    public class CreateAccountResponse : BaseResponse
    {
        public bool email_sent { get; set; }
    }

    public class VerifyCodeResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string secret_key { get; set; }
        public string device_id { get; set; }
        public string nickname { get; set; }
    }

    public class LoginResponse : BaseResponse
    {
        public bool verification_sent { get; set; }
    }

    public class ConfirmLoginResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string secret_key { get; set; }
        public string device_id { get; set; }
        public string nickname { get; set; }
    }

    public class ValidateKeyResponse
    {
        // Direct user object fields returned by API
        public string id { get; set; }
        public string nickname { get; set; }
        public string email { get; set; }
        public string type_name { get; set; }
        public bool is_active { get; set; }
        public bool is_verified { get; set; }
        public int coins { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string last_login_at { get; set; }
        
        // Legacy fields for backward compatibility
        public bool success => is_active && is_verified;
        public string message { get; set; }
        public bool valid => is_active && is_verified;
    }

    public class GenerateGameCodeResponse
    {
        public string code { get; set; }
        public int expires_in_seconds { get; set; }
    }

    public class ModuleStatusResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string status { get; set; } // "published", "unpublished", "banned"
        public string owner_nickname { get; set; }
    }

    public class CheckModuleResponse
    {
        public bool success => can_publish; // Computed property for backward compatibility
        public string message { get; set; }
        public bool can_publish { get; set; }
        public bool is_new { get; set; }
        public bool is_update { get; set; }
        public string existing_module_id { get; set; }
        public string module_id { get; set; } // Alternative field name used by API
        
        // Helper property to determine action
        public string action
        {
            get
            {
                if (!can_publish)
                {
                    // Check if module exists but user can't publish (name taken)
                    if (!string.IsNullOrEmpty(existing_module_id) || !string.IsNullOrEmpty(module_id))
                    {
                        return "not_authorized";
                    }
                    return "not_authorized";
                }
                
                if (is_new)
                {
                    return "create_new";
                }
                else if (is_update)
                {
                    return "update_existing";
                }
                
                return "create_new"; // Default fallback
            }
        }
    }

    public class PublishModuleResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string module_id { get; set; }
        public string version { get; set; }
    }

    public class ContributorsResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string module_id { get; set; }
        public string module_name { get; set; }
        public string owner_nickname { get; set; }
        public List<ContributorInfo> contributors { get; set; }
        
        // Helper property for backward compatibility
        public bool is_owner { get; set; }
    }

    public class ContributorInfo
    {
        public string user_id { get; set; }
        public string nickname { get; set; }
        public bool can_publish { get; set; }
        public string added_at { get; set; }
    }

    #endregion
}
