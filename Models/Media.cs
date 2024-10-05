using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace FamilyApp.API.Models
{
    public class Media
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("persons")]
        public List<string> Persons { get; set; }

        [BsonElement("filePath")]
        public string FilePath { get; set; }

        [BsonElement("fileType")]
        public string FileType { get; set; }

        [BsonElement("story")]
        public string Story { get; set; } 
    }
}
