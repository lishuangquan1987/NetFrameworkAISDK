using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetFrameworkAISDK.Common;

namespace NetFrameworkAISDK.Plugins.Storage
{
    /// <summary>
    /// 文件存储插件，使用 JSON 文件存储会话
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Storage.FileStorage", "1.0.0")]
    [StoragePlugin("File")]
    public class FileStoragePlugin : IStoragePlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Storage.FileStorage"; } }
        public string Name { get { return "File Storage"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Stores conversations in JSON files"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string StorageType { get { return "File"; } }

        private string _baseDirectory;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null && config.Settings.ContainsKey("baseDirectory"))
            {
                _baseDirectory = config.Settings["baseDirectory"] as string;
            }
            else
            {
                _baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NetFrameworkAISDK",
                    "sessions");
            }

            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

        public PluginValidationResult Validate()
        {
            try
            {
                if (!Directory.Exists(_baseDirectory))
                {
                    Directory.CreateDirectory(_baseDirectory);
                }

                var testFile = Path.Combine(_baseDirectory, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return PluginValidationResult.Success();
            }
            catch (Exception ex)
            {
                return PluginValidationResult.Failure("Cannot access storage directory: " + ex.Message);
            }
        }

        public IConversationStore CreateStore(PluginConfig config)
        {
            return new FileConversationStore(_baseDirectory);
        }
    }

    /// <summary>
    /// 文件会话存储实现
    /// </summary>
    public class FileConversationStore : IConversationStore
    {
        private readonly string _baseDirectory;
        private readonly string _sessionsDirectory;
        private readonly string _snapshotsDirectory;
        private readonly object _lock = new object();

        public FileConversationStore(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
            _sessionsDirectory = Path.Combine(_baseDirectory, "sessions");
            _snapshotsDirectory = Path.Combine(_baseDirectory, "snapshots");

            if (!Directory.Exists(_sessionsDirectory))
            {
                Directory.CreateDirectory(_sessionsDirectory);
            }

            if (!Directory.Exists(_snapshotsDirectory))
            {
                Directory.CreateDirectory(_snapshotsDirectory);
            }
        }

        public void SaveSession(string sessionId, List<ConversationMessage> history)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentNullException("sessionId");
            }

            lock (_lock)
            {
                var filePath = GetSessionFilePath(sessionId);
                var metadata = new FileSessionMetadata
                {
                    SessionId = sessionId,
                    MessageCount = history != null ? history.Count : 0,
                    CreatedAt = File.Exists(filePath) ? File.GetCreationTime(filePath) : DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };

                var data = new FileSessionData
                {
                    Metadata = metadata,
                    Messages = history ?? new List<ConversationMessage>()
                };

                var json = JsonHelper.Serialize(data);
                File.WriteAllText(filePath, json);
            }
        }

        public List<ConversationMessage> LoadSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentNullException("sessionId");
            }

            lock (_lock)
            {
                var filePath = GetSessionFilePath(sessionId);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var data = JsonHelper.Deserialize<FileSessionData>(json);

                if (data != null && data.Metadata != null)
                {
                    data.Metadata.LastAccessedAt = DateTime.UtcNow;
                    SaveSessionMetadata(sessionId, data.Metadata);
                }

                return data != null ? data.Messages : null;
            }
        }

        public IEnumerable<SessionInfo> ListSessions()
        {
            lock (_lock)
            {
                var result = new List<SessionInfo>();

                if (!Directory.Exists(_sessionsDirectory))
                {
                    return result;
                }

                var files = Directory.GetFiles(_sessionsDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var data = JsonHelper.Deserialize<FileSessionData>(json);

                        if (data != null && data.Metadata != null)
                        {
                            result.Add(new SessionInfo
                            {
                                SessionId = data.Metadata.SessionId,
                                CreatedAt = data.Metadata.CreatedAt,
                                LastAccessedAt = data.Metadata.LastAccessedAt,
                                MessageCount = data.Metadata.MessageCount,
                                Metadata = ConvertToSessionMetadata(data.Metadata)
                            });
                        }
                    }
                    catch
                    {
                    }
                }

                return result.OrderByDescending(s => s.LastAccessedAt);
            }
        }

        public bool DeleteSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }

            lock (_lock)
            {
                var filePath = GetSessionFilePath(sessionId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var snapshotDir = GetSnapshotDirectory(sessionId);
                if (Directory.Exists(snapshotDir))
                {
                    Directory.Delete(snapshotDir, true);
                }

                return true;
            }
        }

        public void SaveSnapshot(string sessionId, string snapshotName, List<ConversationMessage> history)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(snapshotName))
            {
                throw new ArgumentNullException("sessionId or snapshotName");
            }

            lock (_lock)
            {
                var snapshotDir = GetSnapshotDirectory(sessionId);
                if (!Directory.Exists(snapshotDir))
                {
                    Directory.CreateDirectory(snapshotDir);
                }

                var filePath = Path.Combine(snapshotDir, SanitizeFileName(snapshotName) + ".json");
                var data = new FileSnapshotData
                {
                    SessionId = sessionId,
                    SnapshotName = snapshotName,
                    CreatedAt = DateTime.UtcNow,
                    MessageCount = history != null ? history.Count : 0,
                    Messages = history ?? new List<ConversationMessage>()
                };

                var json = JsonHelper.Serialize(data);
                File.WriteAllText(filePath, json);
            }
        }

        public List<ConversationMessage> LoadSnapshot(string sessionId, string snapshotName)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(snapshotName))
            {
                return null;
            }

            lock (_lock)
            {
                var snapshotDir = GetSnapshotDirectory(sessionId);
                if (!Directory.Exists(snapshotDir))
                {
                    return null;
                }

                var filePath = Path.Combine(snapshotDir, SanitizeFileName(snapshotName) + ".json");
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var data = JsonHelper.Deserialize<FileSnapshotData>(json);
                return data != null ? data.Messages : null;
            }
        }

        public IEnumerable<SnapshotInfo> ListSnapshots(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return new List<SnapshotInfo>();
            }

            lock (_lock)
            {
                var result = new List<SnapshotInfo>();
                var snapshotDir = GetSnapshotDirectory(sessionId);

                if (!Directory.Exists(snapshotDir))
                {
                    return result;
                }

                var files = Directory.GetFiles(snapshotDir, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var data = JsonHelper.Deserialize<FileSnapshotData>(json);

                        if (data != null)
                        {
                            result.Add(new SnapshotInfo
                            {
                                SnapshotName = data.SnapshotName,
                                CreatedAt = data.CreatedAt,
                                MessageCount = data.MessageCount
                            });
                        }
                    }
                    catch
                    {
                    }
                }

                return result.OrderByDescending(s => s.CreatedAt);
            }
        }

        public bool DeleteSnapshot(string sessionId, string snapshotName)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(snapshotName))
            {
                return false;
            }

            lock (_lock)
            {
                var snapshotDir = GetSnapshotDirectory(sessionId);
                if (!Directory.Exists(snapshotDir))
                {
                    return false;
                }

                var filePath = Path.Combine(snapshotDir, SanitizeFileName(snapshotName) + ".json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
        }

        public void UpdateSessionMetadata(string sessionId, SessionMetadata metadata)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            lock (_lock)
            {
                var filePath = GetSessionFilePath(sessionId);
                FileSessionData data;

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    data = JsonHelper.Deserialize<FileSessionData>(json) ?? new FileSessionData();
                }
                else
                {
                    data = new FileSessionData();
                }

                if (data.Metadata == null)
                {
                    data.Metadata = new FileSessionMetadata { SessionId = sessionId };
                }

                data.Metadata.CustomMetadata = metadata;
                data.Metadata.LastAccessedAt = DateTime.UtcNow;

                var updatedJson = JsonHelper.Serialize(data);
                File.WriteAllText(filePath, updatedJson);
            }
        }

        public void Dispose()
        {
        }

        private string GetSessionFilePath(string sessionId)
        {
            return Path.Combine(_sessionsDirectory, SanitizeFileName(sessionId) + ".json");
        }

        private string GetSnapshotDirectory(string sessionId)
        {
            return Path.Combine(_snapshotsDirectory, SanitizeFileName(sessionId));
        }

        private void SaveSessionMetadata(string sessionId, FileSessionMetadata metadata)
        {
            var filePath = GetSessionFilePath(sessionId);
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var data = JsonHelper.Deserialize<FileSessionData>(json);
                if (data != null)
                {
                    data.Metadata = metadata;
                    File.WriteAllText(filePath, JsonHelper.Serialize(data));
                }
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private static SessionMetadata ConvertToSessionMetadata(FileSessionMetadata fileMetadata)
        {
            if (fileMetadata == null)
            {
                return null;
            }

            return new SessionMetadata
            {
                Name = fileMetadata.CustomMetadata != null ? fileMetadata.CustomMetadata.Name : null,
                Description = fileMetadata.CustomMetadata != null ? fileMetadata.CustomMetadata.Description : null,
                Tags = fileMetadata.CustomMetadata != null ? fileMetadata.CustomMetadata.Tags : new List<string>(),
                CustomData = fileMetadata.CustomMetadata != null ? fileMetadata.CustomMetadata.CustomData : new Dictionary<string, string>()
            };
        }

        private class FileSessionData
        {
            public FileSessionMetadata Metadata { get; set; }
            public List<ConversationMessage> Messages { get; set; }
        }

        private class FileSessionMetadata
        {
            public string SessionId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessedAt { get; set; }
            public int MessageCount { get; set; }
            public FileSessionMetadata CustomMetadata { get; set; }
        }

        private class FileSnapshotData
        {
            public string SessionId { get; set; }
            public string SnapshotName { get; set; }
            public DateTime CreatedAt { get; set; }
            public int MessageCount { get; set; }
            public List<ConversationMessage> Messages { get; set; }
        }
    }
}
