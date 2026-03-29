using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataBaseLayer.Models
{
    [PrimaryKey(nameof(Id))]

    public class TelegramUserInfo
    {
        /// <summary>
        /// UserId
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public required bool IsBot { get; set; }
        [MaxLength(512)]
        public required string FirstName { get; set; }
        [MaxLength(128)]
        public string? LastName { get; set; }
        [MaxLength(128)]
        public string? Username { get; set; }
        public bool? IsPremium { get; set; }
        public ChatStrategy PreferredProvider { get; set; }
        [MaxLength(10)]
        public string? LanguageCode { get; set; }
        /// <summary>
        /// Personally selected GPT model for the user.
        /// </summary>
        [MaxLength(128)]
        public string? SelectedModel { get; set; }
    }
}
