using System;
using System.ComponentModel.DataAnnotations;

namespace PunchServerMVC.Utils
{
    public class DateGreaterThanAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public DateGreaterThanAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var currentValue = value as DateTime?;
            var property = validationContext.ObjectType.GetProperty(_comparisonProperty);

            if (property == null)
                return new ValidationResult($"Unknown property: {_comparisonProperty}");

            var comparisonValue = property.GetValue(validationContext.ObjectInstance) as DateTime?;

            if (currentValue.HasValue && comparisonValue.HasValue && currentValue < comparisonValue)
            {
                return new ValidationResult(ErrorMessage ??
                    $"{validationContext.DisplayName} must be greater than or equal to {_comparisonProperty}.");
            }

            return ValidationResult.Success;
        }
    }
}
