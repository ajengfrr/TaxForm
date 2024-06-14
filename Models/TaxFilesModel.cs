using System.ComponentModel.DataAnnotations;

namespace TaxForm.Models
{
    public class TaxFilesModel
    {
        [Required(ErrorMessage = "Please select files")]
        //public IFormFile Files { get; set; }
        public List<IFormFile> Files { get; set; }
        public bool ShowMessage { get; set; }
        //public string Message { get; set; }
    }
}
