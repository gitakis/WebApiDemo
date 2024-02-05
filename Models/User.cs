using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

namespace UserManagementService.Models
{
    public class User
    {
        private string? _password;

        public int Id { get; set; }

        [Required]
        public required string UserName { get; set; }

        public string? FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? MobileNumber { get; set; }

        public string? Language { get; set; }

        public string? Culture { get; set; }

        public string? Password {
            get {
                return _password;
            }
            set {
                _password = Encrypt(value == null ? "" : value);
            }  
        }

        public string Encrypt(string value) {
            using (SHA256 encrypter = SHA256.Create())
            {
                return BitConverter.ToString(encrypter.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }

        public bool ValidatePassword(string password)
        {
            return (this.Password == Encrypt(password));
        }
    }
}
