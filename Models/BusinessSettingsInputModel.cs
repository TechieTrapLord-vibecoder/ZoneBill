using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ZoneBill_Lloren.Models
{
    public class BusinessSettingsInputModel
    {
        public int BusinessId { get; set; }

        [Required]
        public string BusinessName { get; set; } = string.Empty;

        public decimal TaxRatePercentage { get; set; }

        public decimal InitialCapital { get; set; }

        public string ThemePreference { get; set; } = "Nightlife";

        public bool InventoryAlertEnabled { get; set; }

        [EmailAddress]
        public string? InventoryAlertEmail { get; set; }

        public int InventoryReorderLookbackDays { get; set; }

        public int InventoryLeadTimeDays { get; set; }

        public int InventorySafetyStockDays { get; set; }

        public int InventoryTargetCoverageDays { get; set; }

        public int InventoryForecastLookbackDays { get; set; }

        public int InventoryForecastHorizonDays { get; set; }

        public IFormFile? LogoFile { get; set; }
    }
}