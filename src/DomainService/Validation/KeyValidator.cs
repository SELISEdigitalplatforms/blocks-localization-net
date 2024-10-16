using FluentValidation;

namespace DomainService.Services
{
    public class KeyValidator : AbstractValidator<Key>
    {
        public KeyValidator()
        {
            // Validate KeyName
            RuleFor(key => key.KeyName)
                .NotEmpty().WithMessage("KeyName is required.")
                .Length(3, 100).WithMessage("KeyName must be between 3 and 100 characters long.");

            // Validate Module
            RuleFor(key => key.Module)
                .NotEmpty().WithMessage("Module is required.")
                .Length(2, 50).WithMessage("Module must be between 2 and 50 characters long.");

            // Validate Value
            RuleFor(key => key.Value)
                .NotEmpty().WithMessage("Value is required.")
                .MaximumLength(500).WithMessage("Value can be up to 500 characters long.");

            //// Validate Translations
            //RuleFor(key => key.Translations)
            //    .Must(translations => translations != null && translations.Count > 0)
            //    .WithMessage("Translations must contain at least one entry.");

            //// Validate Routes
            //RuleFor(key => key.Routes)
            //    .Must(routes => routes != null && routes.Count > 0)
            //    .WithMessage("Routes must contain at least one route.")
            //    .ForEach(route => route.NotEmpty().WithMessage("Each route must be non-empty."));
        }
    }
}
