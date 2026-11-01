using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class LLMService
    {
        private readonly HttpClient _httpClient;
        private string _baseEndpoint = "http://127.0.0.1:5001";

        public LLMService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public void SetEndpoint(string endpoint)
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                _baseEndpoint = endpoint.TrimEnd('/');
            }
        }

        public async Task<bool> CheckHealthAsync(string? customEndpoint = null)
        {
            string url = (customEndpoint ?? _baseEndpoint).TrimEnd('/');
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                var response = await _httpClient.GetAsync($"{url}/api/v1/model", cts.Token);
                if (response.IsSuccessStatusCode) return true;

                using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                var response2 = await _httpClient.GetAsync($"{url}/v1/models", cts2.Token);
                return response2.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetActiveModelNameAsync(string? customEndpoint = null)
        {
            string url = (customEndpoint ?? _baseEndpoint).TrimEnd('/');
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                var response = await _httpClient.GetAsync($"{url}/api/v1/model", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("result", out var res))
                    {
                        return res.GetString() ?? "Loaded Model";
                    }
                }

                using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                var response2 = await _httpClient.GetAsync($"{url}/v1/models", cts2.Token);
                if (response2.IsSuccessStatusCode)
                {
                    string json2 = await response2.Content.ReadAsStringAsync();
                    using var doc2 = JsonDocument.Parse(json2);
                    if (doc2.RootElement.TryGetProperty("data", out var dataArr) && dataArr.GetArrayLength() > 0)
                    {
                        return dataArr[0].GetProperty("id").GetString() ?? "Model";
                    }
                }
            }
            catch
            {
            }
            return "Offline / Disconnected";
        }

        public async IAsyncEnumerable<string> StreamChatCompletionAsync(
            List<ChatMessage> messages,
            ChatSession session,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string url = $"{_baseEndpoint}/v1/chat/completions";

            var msgList = new List<object>();
            foreach (var m in messages)
            {
                msgList.Add(new { role = m.Role.ToLowerInvariant(), content = m.Content });
            }

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = "default",
                ["messages"] = msgList,
                ["stream"] = true,
                ["temperature"] = session.Temperature,
                ["top_p"] = session.TopP,
                ["min_p"] = session.MinP,
                ["repetition_penalty"] = session.RepetitionPenalty,
                ["dry_multiplier"] = session.DryMultiplier,
                ["dry_base"] = session.DryBase,
                ["dry_allowed_length"] = session.DryAllowedLength,
                ["max_tokens"] = session.MaxTokens
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage? response = null;
            string? connectionError = null;

            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception ex)
            {
                connectionError = $"[Error connecting to LLM Backend at {_baseEndpoint}: {ex.Message}]";
            }

            if (connectionError != null)
            {
                yield return connectionError;
                yield break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                string err = response != null ? await response.Content.ReadAsStringAsync(cancellationToken) : "No response";
                yield return $"[LLM Error: {err}]";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    string data = line.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    string? token = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentProp))
                            {
                                token = contentProp.GetString();
                            }
                            else if (firstChoice.TryGetProperty("text", out var textProp))
                            {
                                token = textProp.GetString();
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        yield return token;
                    }
                }
            }
        }

        public async IAsyncEnumerable<string> StreamChatCompletionAsync(
            List<ChatMessage> messages,
            double temperature = 1.05,
            double minP = 0.07,
            double topP = 0.95,
            double repetitionPenalty = 1.08,
            double dryMultiplier = 0.8,
            int maxTokens = 1024,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var fakeSession = new ChatSession
            {
                Temperature = temperature,
                MinP = minP,
                TopP = topP,
                RepetitionPenalty = repetitionPenalty,
                DryMultiplier = dryMultiplier,
                MaxTokens = maxTokens
            };

            await foreach (var token in StreamChatCompletionAsync(messages, fakeSession, cancellationToken))
            {
                yield return token;
            }
        }

        public async IAsyncEnumerable<string> StreamTextCompletionAsync(
            string prompt,
            double temperature = 1.05,
            double minP = 0.07,
            double topP = 0.95,
            double repetitionPenalty = 1.08,
            double dryMultiplier = 0.8,
            int maxTokens = 1024,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string url = $"{_baseEndpoint}/v1/completions";

            var stopTokens = new List<string>
            {
                "<|im_end|>",
                "<|im_start|>",
                "<|endoftext|>",
                "</s>",
                "\nSincerely,",
                "\nDear [",
                "\n[Response]:",
                "\nEnd Output.",
                "\n[/Generated Text]",
                "\nI appreciate your thoughtful",
                "\nThank you for choosing to work with me",
                "\nHuman:",
                "\nUser:",
                "\nAssistant:"
            };

            var requestBody = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["stream"] = true,
                ["temperature"] = temperature,
                ["top_p"] = topP,
                ["min_p"] = minP,
                ["repetition_penalty"] = repetitionPenalty,
                ["dry_multiplier"] = dryMultiplier,
                ["max_tokens"] = maxTokens,
                ["stop"] = stopTokens
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage? response = null;
            string? connectionError = null;

            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception ex)
            {
                connectionError = $"[Error connecting to LLM Backend at {_baseEndpoint}: {ex.Message}]";
            }

            if (connectionError != null)
            {
                yield return connectionError;
                yield break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                string err = response != null ? await response.Content.ReadAsStringAsync(cancellationToken) : "No response";
                yield return $"[LLM Error: {err}]";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    string data = line.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    string? token = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("text", out var textProp))
                            {
                                token = textProp.GetString();
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        yield return token;
                    }
                }
            }
        }
    }
}
