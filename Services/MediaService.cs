﻿using FamilyApp.API.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FamilyApp.API.Services
{
    public class MediaService
    {
        private readonly IMongoCollection<Media> _media;
        private readonly DriveService _driveService;
        private readonly string _sharedFolderId; // Google Drive folder ID for storing media
        private readonly EncryptionService _encryptionService;
        private readonly string _serverBaseUrl; // Base URL for serving files
        private readonly ILogger<MediaService> _logger; // Add logger

        public MediaService(IMongoClient client, string serverBaseUrl, string sharedFolderId, string serviceAccountJsonFilePath, EncryptionService encryptionService, ILogger<MediaService> logger)
        {
            var database = client.GetDatabase("MyFamilyApp");
            _media = database.GetCollection<Media>("Media");
            _serverBaseUrl = serverBaseUrl;
            _sharedFolderId = sharedFolderId;
            _encryptionService = encryptionService;
            _logger = logger; // Initialize logger

            // Authenticate using the service account
            var credential = GoogleCredential.FromFile(serviceAccountJsonFilePath)
                .CreateScoped(DriveService.ScopeConstants.DriveFile);

            // Initialize Google Drive service
            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "MyFamilyApp",
            });
        }

        public async Task<Media> AddMediaAsync(string description, List<string> persons, byte[] fileData, string filename, string fileType, string story)
        {
            try
            {
                _logger.LogInformation("Adding new media file: {Filename}", filename);

                var encodedDescription = _encryptionService.Encrypt(description);
                var encodedPersons = persons?.ConvertAll(person => _encryptionService.Encrypt(person)) ?? new List<string>();
                var encodedStory = _encryptionService.Encrypt(story);

                // Upload file to Google Drive
                var fileId = await UploadFileToGoogleDrive(fileData, filename);

                var media = new Media
                {
                    Description = encodedDescription,
                    Persons = encodedPersons,
                    FilePath = fileId, // Store the Google Drive file ID
                    FileType = fileType,
                    Story = encodedStory
                };

                await _media.InsertOneAsync(media);
                _logger.LogInformation("Media file {Filename} added successfully with File ID: {FileId}", filename, fileId);

                return media;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding media file: {Filename}", filename);
                throw;
            }
        }

        public async Task<Stream> GetMediaStreamAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Fetching media stream for file ID: {FileId}", fileId);

                var request = _driveService.Files.Get(fileId);
                var stream = new MemoryStream();
                await request.DownloadAsync(stream);
                stream.Position = 0; // Reset stream position
                _logger.LogInformation("Media stream fetched successfully for file ID: {FileId}", fileId);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching media stream for file ID: {FileId}", fileId);
                return null;
            }
        }

        public async Task<Media> GetMediaByIdAsync(string id)
        {
            _logger.LogInformation("Fetching media by ID: {MediaId}", id);
            var media = await _media.Find(m => m.FilePath == id).FirstOrDefaultAsync();
            return DecodeMedia(media);
        }

        public async Task<PaginatedList<MediaDTO>> GetPaginatedMediaAsync(int pageNumber, int pageSize)
        {
            _logger.LogInformation("Fetching paginated media - Page {PageNumber}, Page Size {PageSize}", pageNumber, pageSize);

            var totalRecords = await _media.CountDocumentsAsync(Builders<Media>.Filter.Empty);
            var mediaList = await _media.Find(Builders<Media>.Filter.Empty)
                                        .Skip((pageNumber - 1) * pageSize)
                                        .Limit(pageSize)
                                        .ToListAsync();

            var mediaDTOs = mediaList.Select(m => new MediaDTO
            {
                Id = m.Id.ToString(),
                Description = _encryptionService.Decrypt(m.Description ?? string.Empty),
                Persons = m.Persons != null ? m.Persons.ConvertAll(person => _encryptionService.Decrypt(person)) : new List<string>(),
                FileType = m.FileType,
                FilePath = m.FilePath,
                FileUrl = $"{_serverBaseUrl}/api/media/download/{m.FilePath}",
                Story = _encryptionService.Decrypt(m.Story ?? string.Empty)
            }).ToList();

            _logger.LogInformation("Paginated media fetched successfully.");
            return new PaginatedList<MediaDTO>(mediaDTOs, (int)totalRecords, pageNumber, pageSize);
        }

        public async Task<PaginatedList<MediaDTO>> SearchMediaByPersonAsync(string person, int pageNumber, int pageSize)
        {
            _logger.LogInformation("Searching media by person: {Person}", person);

            var allMedia = await _media.Find(Builders<Media>.Filter.Empty).ToListAsync();
            var filteredMedia = allMedia
                .Where(m => m.Persons != null && m.Persons.Any(p => _encryptionService.Decrypt(p).IndexOf(person, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            var paginatedMedia = filteredMedia
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mediaDTOs = paginatedMedia.Select(m => new MediaDTO
            {
                Id = m.Id.ToString(),
                Description = _encryptionService.Decrypt(m.Description ?? string.Empty),
                Persons = m.Persons != null ? m.Persons.ConvertAll(p => _encryptionService.Decrypt(p)) : new List<string>(),
                FileType = m.FileType,
                FilePath = m.FilePath,
                FileUrl = $"{_serverBaseUrl}/api/media/download/{m.FilePath}",
                Story = _encryptionService.Decrypt(m.Story ?? string.Empty)
            }).ToList();

            _logger.LogInformation("Media search results returned for person: {Person}", person);
            return new PaginatedList<MediaDTO>(mediaDTOs, filteredMedia.Count, pageNumber, pageSize);
        }

        public async Task DeleteMediaAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Deleting media file with ID: {FileId}", fileId);

                var media = await _media.Find(m => m.FilePath == fileId).FirstOrDefaultAsync();
                if (media == null)
                {
                    _logger.LogWarning("Media file not found for ID: {FileId}", fileId);
                    return;
                }

                // Delete file from Google Drive
                await _driveService.Files.Delete(media.FilePath).ExecuteAsync();
                await _media.DeleteOneAsync(m => m.FilePath == fileId);

                _logger.LogInformation("Media file with ID {FileId} deleted successfully.", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media file with ID: {FileId}", fileId);
                throw;
            }
        }

        private async Task<string> UploadFileToGoogleDrive(byte[] fileData, string filename)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = filename,
                Parents = new List<string> { _sharedFolderId }
            };

            try
            {
                _logger.LogInformation("Uploading file to Google Drive: {Filename}", filename);

                using (var stream = new MemoryStream(fileData))
                {
                    var request = _driveService.Files.Create(fileMetadata, stream, "application/octet-stream");
                    request.Fields = "id";
                    var result = await request.UploadAsync();

                    if (result.Status == UploadStatus.Failed)
                    {
                        throw new Exception($"File upload failed: {result.Exception?.Message}");
                    }

                    _logger.LogInformation("File uploaded to Google Drive successfully: {Filename}", filename);
                    return request.ResponseBody.Id;  // Return the file ID from Google Drive
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Google Drive: {Filename}", filename);
                throw;
            }
        }

        private Media DecodeMedia(Media media)
        {
            if (media == null) return null;
            media.Description = _encryptionService.Decrypt(media.Description ?? string.Empty);
            media.Persons = media.Persons != null ? media.Persons.ConvertAll(person => _encryptionService.Decrypt(person)) : new List<string>();
            media.Story = _encryptionService.Decrypt(media.Story ?? string.Empty);
            return media;
        }
    }
}
