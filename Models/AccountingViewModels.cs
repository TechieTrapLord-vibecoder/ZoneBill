using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ZoneBill_Lloren.Models
{
    public class JournalEntryLineEditorViewModel
    {
        public int? JournalLineId { get; set; }
        public int? AccountId { get; set; }
        [Range(0, 999999999)] public decimal Debit { get; set; }
        [Range(0, 999999999)] public decimal Credit { get; set; }
    }

    public class JournalEntryEditorViewModel
    {
        public int? JournalEntryId { get; set; }
        public int BusinessId { get; set; }
        public int? ReferenceId { get; set; }
        [MaxLength(50)] public string? ReferenceType { get; set; }
        public DateTime EntryDate { get; set; }
        [MaxLength(255)] public string? Description { get; set; }
        public List<JournalEntryLineEditorViewModel> Lines { get; set; } = new();
        public List<SelectListItem> AccountOptions { get; set; } = new();
    }
}